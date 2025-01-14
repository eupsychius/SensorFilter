using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace SensorFilter
{
    public class FileProcessor
    {
        private        readonly DatabaseHelper  databaseHelper  = new();
        private static readonly SensorModels    sensorModels    = new();

        // Получаем строки файла на парсинг
        public bool ParseCharacterisationData(string[] lines, string dbPath, string fileName)
        {
            /// Инт на проверку дубликатов
            /// 0 - Файл не дублирован
            /// 1 - Некоторые сведения уже имеются в ДБ
            /// 2 - Повторяющийся файл  
            int fileDupe = 0;

            // Данные парсинга датчика
            string  sensorInfo      = lines[1];
            var     sensorDetails   = sensorInfo.Split(';');

            string  serialNumber    =                               sensorDetails[1].Split(':')[1].Trim();
            string  type            = sensorDetails.Length > 2 ?    sensorDetails[2].Split(':')[1].Trim() : "-";
            string  model           = sensorDetails.Length > 3 ?    sensorDetails[3].Split(':')[1].Trim() : "-";

            if (type == "-" || model == "-") ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл содержит неполные сведения о датчике");

            using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                connection.Open();
                int sensorId = DatabaseHelper.InsertSensorInfo(serialNumber, type, model, connection);

                if (databaseHelper.CheckCharacterisationExists(sensorId, connection)) fileDupe = 1;

                var sensorCharacterisationList  = new List<SensorCharacterisation>();
                var sensorCoefficientList       = new List<SensorCoefficients>();

                bool coefficientsSection    = false;
                int coefficientCount        = 0;
                DateTime date               = DateTime.MinValue;

                for (int line = 4; line < lines.Length; line++)
                {
                    if (lines[line].Contains("Коэффициенты датчика"))
                    {
                        coefficientsSection = true;
                        coefficientCount    = int.Parse(lines[++line].Split(':')[1].Trim());
                        line++;
                    }

                    if (!coefficientsSection)
                    {
                        var data = lines[line].Split('|');
                        if (data.Length < 7) continue;

                        DateTime    dateTime    =   DateTime.   Parse(data[0].Trim());
                        double      temperature =   double.     Parse(data[1].Trim());
                        int         range       =   int.        Parse(data[2].Trim());
                        double      pressure    =   double.     Parse(data[3].Trim());
                        double      voltage     =   double.     Parse(data[4].Trim());
                        double      resistance  =   double.     Parse(data[5].Trim());
                        double      deviation;
                        try {       deviation   =   double.     Parse(data[6].Trim()); }
                        catch {     deviation   =   0.0; }

                        if (databaseHelper.CheckCharacterisationStringExists(sensorId, dateTime.ToString("yyyy-MM-dd HH:mm:ss"), connection))
                        if (fileDupe == 1) if (databaseHelper.CheckCharacterisationStringExists(
                                sensorId, 
                                dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                connection)) fileDupe = 2;

                        sensorCharacterisationList.Add(new SensorCharacterisation
                        {
                            SensorId    = sensorId,
                            DateTime    = dateTime,
                            Temperature = temperature,
                            Range       = range,
                            Pressure    = pressure,
                            Voltage     = voltage,
                            Resistance  = resistance,
                            Deviation   = deviation
                        });

                        date = dateTime;
                    }
                    else
                    {
                        var coefficientData = lines[line].Split(':');
                        if (coefficientData.Length < 2) continue;

                        int     coefficientIndex = int.     Parse(coefficientData[0].Trim());
                        double  coefficientValue = double.  Parse(coefficientData[1].Trim());

                        if (databaseHelper.CheckCoefficientStringExists(sensorId, coefficientIndex.ToString(), date.ToString("yyyy-MM-dd HH:mm:ss"), connection))
                            fileDupe = 2;

                        sensorCoefficientList.Add(new SensorCoefficients
                        {
                            SensorId            = sensorId,
                            CoefficientIndex    = coefficientIndex,
                            CoefficientValue    = coefficientValue,
                            CoefficientsDate    = date
                        });
                    }
                }

                // Если словили дублирующуюся строку, то сведения не заносятся
                if (fileDupe == 2) ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл содержит дубликаты строк");
                else
                {
                    if (fileDupe == 1) ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Часть данных уже есть в базе данных");

                    try
                    {
                        databaseHelper.InsertSensorCharacterisationBulk(sensorCharacterisationList, connection);
                        if (sensorCoefficientList.Count > 0)
                            databaseHelper.InsertSensorCoefficientsBulk(sensorCoefficientList, connection);
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError(fileName, "ОШИБКА", ex.Message);
                    }
                }
            }

            // Для возврата количества дубликатов
            return fileDupe == 2;
        }

        public bool ParseVerificationData(string[] lines, string dbPath, string fileName)
        {
            /// Инт на проверку дубликатов
            /// 0 - Файл не дублирован
            /// 1 - Некоторые сведения уже имеются в ДБ
            /// 2 - Повторяющийся файл  
            int fileDupe = 0;

            var verificationDataList = new List<SensorVerification>(); // Коллекция для пакетной вставки данных верификации

            // Парсим информацию о датчике из второй строки
            string  sensorInfo      = lines[1];
            var     sensorDetails   = sensorInfo.Split(';');

            string  serialNumber    =                               sensorDetails[1].Split(':')[1].Trim();
            string  type            = sensorDetails.Length > 2 ?    sensorDetails[2].Split(':')[1].Trim() : "-";
            string  model           = sensorDetails.Length > 3 ?    sensorDetails[3].Split(':')[1].Trim() : "-";

            if (type == "-" || model == "-") ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл содержит неполные сведения о датчике");

            // Конвертим нестандартную модель в общую
            if (type.Contains("ЭнИ-100") || type.Contains("ЭНИ-100"))   type = "ЭнИ-100";
            if (type == "ЭНИ-12")                                       type = "ЭнИ-12";
            if (type == "ЭНИ-12М")                                      type = "ЭнИ-12М";

            if (model != "-" &&
                ((( type == "ЭнИ-100"   ) && !sensorModels.EnI100_Models(). Contains(model)) ||
                ((  type == "ЭнИ-12"    ) && !sensorModels.EnI12_Models().  Contains(model)) ||
                ((  type == "ЭнИ-12М"   ) && !sensorModels.EnI12M_Models(). Contains(model)) ))
                ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", $"Файл содержит нестандартную модель - {model} ({type})");

            using (SQLiteConnection connection = new($"Data Source={dbPath};Version=3;"))
            {
                connection.Open();

                /*Булы для одноразовой отправки предупреждений
                bool verDupeWarned      = false;
                bool verNoVoltWarned    = false;
                bool verNoResiWarned    = false;
                bool defectPressureWarned   = false;
                bool defectCurrentWarned    = false;*/

                // Вставляем информацию о сенсоре
                int sensorId = DatabaseHelper.InsertSensorInfo(serialNumber, type, model, connection);

                // Проверяем наличие дубликатов по серийному номеру и дате
                if (databaseHelper.CheckVerificationExists(sensorId, connection)) fileDupe = 1;

                // Парсим данные верификации, начиная с 5 строки
                for (int line = 5; line < lines.Length; line++)
                {
                    // Отлов пустых строк
                    if (string.IsNullOrEmpty(lines[line])) continue;

                    var data = lines[line].Split('|');

                    DateTime    dateTime        =   DateTime.   Parse(data[0].Trim());
                    double      temperature     =   double.     Parse(data[1].Trim());
                    double      npi             =   double.     Parse(data[2].Trim());
                    double      vpi             =   double.     Parse(data[3].Trim());
                    double      pressureGiven   =   double.     Parse(data[4].Trim());
                    double      pressureReal    =   double.     Parse(data[5].Trim());
                    double      currentGiven    =   double.     Parse(data[6].Trim());
                    double      currentReal     =   double.     Parse(data[7].Trim());
                    double      voltage;
                    try {       voltage         =   double.     Parse(data[8].Trim()); }
                    catch {     voltage         =   0.0; }
                    double      resistance;
                    try {       resistance      =   double.     Parse(data[9].Trim()); }
                    catch {     resistance      =   0.0; }

                    /* Проверяем отсутствие напряжения
                    if (!verNoVoltWarned)
                    {
                        ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл не содержит данных по напряжению");
                        verNoVoltWarned = true;
                    }*/
                    /* Проверяем отсутствие сопротивления
                    if (!verNoResiWarned)
                    {
                        ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл не содержит данных по сопротивлению");
                        verNoResiWarned = true;
                    }*/
                    /* Проверка на дефектные показатели
                    if ((Math.Abs(pressureReal - pressureGiven) > (0.1 + 0.1 * pressureGiven)) && 
                        !defectPressureWarned)
                    {
                        ErrorLogger.LogErrorAsync(fileName, "БРАК", "Превышено отклонение фактического давления");
                        defectPressureWarned = true;
                    }
                    if ((Math.Abs(currentReal - currentGiven) > 0.05) && 
                        !defectCurrentWarned)
                    {
                        ErrorLogger.LogErrorAsync(fileName, "БРАК", "Превышено отклонение фактического тока");
                        defectCurrentWarned = true;
                    }*/

                    if (fileDupe == 1)
                        if (databaseHelper.CheckVerificationStringExists(
                            sensorId,
                            model,
                            dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            connection))
                            fileDupe = 2;

                    verificationDataList.Add(new SensorVerification
                    {
                        SensorId        = sensorId,
                        DateTime        = dateTime,
                        Temperature     = temperature,
                        NPI             = npi,
                        VPI             = vpi,
                        PressureGiven   = pressureGiven,
                        PressureReal    = pressureReal,
                        CurrentGiven    = currentGiven,
                        CurrentReal     = currentReal,
                        Voltage         = voltage,
                        Resistance      = resistance
                    });
                }

                // Если словили дублирующуюся строку, то сведения не заносятся
                if (fileDupe == 2) ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл верификации содержит дубликаты строк");
                else
                {
                    // Если некоторые сведения о характеризации имеются, то логируем предупреждение
                    if (fileDupe == 1) ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Сведения о верификации уже есть в базе данных");

                    // Вставляем все данные верификации пакетом
                    try     
                    { 
                        databaseHelper.InsertVerificationDataBulk(verificationDataList, connection); 
                    }
                    catch (Exception ex)   
                    { 
                        ErrorLogger.LogError(fileName, "ОШИБКА", ex.Message); 
                    }
                }
            }

            return fileDupe == 2;
        }
    }
}
