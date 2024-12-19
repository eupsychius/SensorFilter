using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;

namespace SensorFilter
{
    public class FileProcessor
    {
        private                 DatabaseHelper  databaseHelper  = new();
        private static readonly SensorModels    sensorModels    = new();

        // Получаем строки файла на парсинг
        public bool ParseCharacterisationData(string[] lines, string dbPath, string fileName)
        {
            /// Инт на проверку дубликатов
            /// 0 - Файл не дублирован
            /// 1 - Некоторые сведения уже имеются в ДБ
            /// 2 - Повторяющийся файл  
            int fileDupe = 0;

            // Данные парсинга
            string  sensorInfo      = lines[1];
            var     sensorDetails   = sensorInfo.                   Split(';');
            int     channel         = int.Parse(sensorDetails[0].   Split(':')[1].Trim());
            string  serialNumber    = sensorDetails[1].             Split(':')[1].Trim();
            // Некоторые серийники идут без типа и модели
            string  type            = "-";
            try {   type            = sensorDetails[2].             Split(':')[1].Trim(); }
            catch { type            = "-"; }
            string  model           = "-";
            try {   model           = sensorDetails[3].             Split(':')[1].Trim(); }
            catch { model           = "-"; }

            if (type == "-" || model == "-")
                ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл содержит неполные сведения о датчике");

            // Конвертим нестандартную модель в общую
            if (type.Contains("ЭнИ-100") || type.Contains("ЭНИ-100")) type = "ЭНИ-100";

            // Ищем нестандартную модель
            if (
                model != "-" &&
                ((( type == "ЭНИ-100"   ) && !sensorModels.EnI100_Models(). Contains(model)) ||
                ((  type == "ЭНИ-12"    ) && !sensorModels.EnI12_Models().  Contains(model)) ||
                ((  type == "ЭНИ-12М"   ) && !sensorModels.EnI12M_Models(). Contains(model))))
                ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", $"Файл содержит нестандартную модель - {model} ({type})");

            var sensorDataList          = new List<SensorData>();           // Коллекция для пакетной вставки характеризации
            var sensorCoefficientList   = new List<SensorCoefficients>();   // Коллекция для пакетной вставки коэффициентов

            // Открываем соединение для вставки сведений о датчике
            using (SQLiteConnection connection = new($"Data Source={dbPath};Version=3;"))
            {
                connection.Open();

                bool    coefficientsSection = false;
                int     coefficientCount    = 0;

                // Вставляем информацию о сенсоре
                databaseHelper.InsertSensorData(channel, serialNumber, type, model, connection);

                // Проверяем наличие дубликатов по серийному номеру и дате
                if (databaseHelper.CheckCharacterisationExists(serialNumber, type, model, connection))
                    fileDupe = 1;

                DateTime date = new();

                /* Булы для одноразовой отправки предупреждений
                bool charDupeWarned     = false;
                bool charNoDeviWarned   = false;
                bool coefDupeWarned     = false;*/

                // Парсим данные характеризации (начинаются с 5 строки)
                for (int line = 4; line < lines.Length; line++)
                {
                    if (lines[line].Contains("Коэффициенты датчика"))
                    {
                        coefficientsSection = true;
                        line += 1; // Переходим на строку с количеством коэффициентов
                        coefficientCount = int.Parse(lines[line].Split(':')[1].Trim());
                        line += 1; // Переходим на первую строку с коэффициентами
                    }
                    if (!coefficientsSection) // Запись характеризации
                    {
                        if (fileDupe == 2) continue;

                        var data = lines[line].Split('|');
                        if (data.Length < 7) continue;
                        DateTime    dateTime    = DateTime. Parse(data[0].Trim());
                        double      temperature = double.   Parse(data[1].Trim());
                        int         range       = int.      Parse(data[2].Trim());
                        double      pressure    = double.   Parse(data[3].Trim());
                        double      voltage     = double.   Parse(data[4].Trim());
                        double      resistance  = double.   Parse(data[5].Trim());
                        double      deviation   = 0.0;      // В некоторых файлах может отсутствовать
                        try {       deviation   = double.   Parse(data[6].Trim()); }
                        catch {     deviation   = 0.0; }

                        date = dateTime;

                        /* Проверяем отсутствие отклонения
                        if (!charNoDeviWarned)
                        {
                            ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл не содержит данных по отклонению");
                            charNoDeviWarned = true;
                        }*/

                        if (fileDupe == 1)
                            if (databaseHelper.CheckCharacterisationStringExists(
                                serialNumber, 
                                dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                connection))
                                fileDupe = 2;

                        // Собираем данные для пакетной вставки
                        sensorDataList.Add(new SensorData
                        {
                            SerialNumber    = serialNumber,
                            Type            = type,
                            Model           = model,
                            DateTime        = dateTime,
                            Temperature     = temperature,
                            Range           = range,
                            Pressure        = pressure,
                            Voltage         = voltage,
                            Resistance      = resistance,
                            Deviation       = deviation
                        });
                    }
                    if (coefficientsSection)
                    {
                        int actualCoefficientCount = Math.Min(coefficientCount, lines.Length - line);

                        /* Проверяем несоответствие коэффициентов
                        if (coefficientCount != actualCoefficientCount)
                            ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Заявленное кол-во коэффициентов не соответствует реальному");*/

                        // Проверяем наличие дубликатов коэффициентов
                        if (fileDupe == 0)
                            if (databaseHelper.CheckCoefficientExists(serialNumber, type, model, connection))
                                fileDupe = 1;

                        for (int i = 0; i < actualCoefficientCount; i++, line++)
                        {
                            string[] coefficientData = lines[line].Split(':');

                            // Если данных не хватает, пропускаем эту строку
                            if (coefficientData.Length < 2) continue;

                            if (!int.   TryParse(coefficientData[0].Trim(), out int     coefficientIndex) ||
                                !double.TryParse(coefficientData[1].Trim(), out double  coefficientValue))
                                // Пропуск некорректных строк, чтобы избежать исключений
                                continue;

                            DateTime coefficientsDate = date;

                            if (fileDupe == 1)
                                if (databaseHelper.CheckCoefficientStringExists(
                                serialNumber,
                                Convert.ToString(coefficientIndex),
                                coefficientsDate.ToString("yyyy-MM-dd HH:mm:ss"),
                                connection))
                                fileDupe = 2;
                            if (fileDupe == 2)
                                continue;

                            sensorCoefficientList.Add(new SensorCoefficients
                            {
                                SerialNumber        = serialNumber,
                                Type                = type,
                                Model               = model,
                                CoefficientIndex    = coefficientIndex,
                                CoefficientValue    = coefficientValue,
                                CoefficientsDate    = coefficientsDate
                            });
                        }
                    }
                }

                // Если словили дублирующуюся строку, то сведения не заносятся
                if (fileDupe == 2)
                    ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл характеризации содержит дубликаты строк");
                else
                {
                    // Если некоторые сведения о характеризации имеются, то логируем предупреждение
                    if (fileDupe == 1)
                        ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Сведения о характеризации уже есть в базе данных");

                    // Заносим сведения о датчике
                    try { databaseHelper.InsertSensorDataBulk(sensorDataList, connection); }
                    catch { ErrorLogger.LogError(fileName, "ОШИБКА", "Не удалось выгрузить данные характеризации в базу данных"); }
                    if (sensorCoefficientList.Count > 0)
                        try { databaseHelper.InsertSensorCoefficientsBulk(sensorCoefficientList, connection); }
                        catch { ErrorLogger.LogError(fileName, "ОШИБКА", "Не удалось выгрузить данные коэффициентов в базу данных"); }
                }
            }

            // Для возврата количества дубликатов
            bool skippedFile = fileDupe == 2;

            return skippedFile;
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
            var     sensorDetails   = sensorInfo.                   Split(';');
            int     channel         = int.Parse(sensorDetails[0].   Split(':')[1].Trim());
            string  serialNumber    = sensorDetails[1].             Split(':')[1].Trim();
            string  type            = "-";
            try {   type            = sensorDetails[2].             Split(':')[1].Trim(); }
            catch { type            = "-"; }
            string  model           = "-";
            try {   model           = sensorDetails[3].             Split(':')[1].Trim(); }
            catch { model           = "-"; }

            if (type == "-" || model == "-")
                ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл содержит неполные сведения о датчике");

            // Конвертим нестандартную модель в общую
            if (type.Contains("ЭнИ-100") || type.Contains("ЭНИ-100")) type = "ЭНИ-100";

            if (
                model != "-" &&
                ((( type == "ЭНИ-100"   ) && !sensorModels.EnI100_Models(). Contains(model)) ||
                ((  type == "ЭНИ-12"    ) && !sensorModels.EnI12_Models().  Contains(model)) ||
                ((  type == "ЭНИ-12М"   ) && !sensorModels.EnI12M_Models(). Contains(model)) ))
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
                databaseHelper.InsertSensorData(channel, serialNumber, type, model, connection);

                // Проверяем наличие дубликатов по серийному номеру и дате
                if (databaseHelper.CheckVerificationExists(serialNumber, type, model, connection))
                    fileDupe = 1;

                // Парсим данные верификации, начиная с 5 строки
                for (int line = 5; line < lines.Length; line++)
                {
                    // Отлов пустых строк
                    if (string.IsNullOrEmpty(lines[line])) continue;

                    var data = lines[line].Split('|');

                    DateTime    dateTime        = DateTime. Parse(data[0].Trim());
                    double      temperature     = double.   Parse(data[1].Trim());
                    double      npi             = double.   Parse(data[2].Trim());
                    double      vpi             = double.   Parse(data[3].Trim());
                    double      pressureGiven   = double.   Parse(data[4].Trim());
                    double      pressureReal    = double.   Parse(data[5].Trim());
                    double      currentGiven    = double.   Parse(data[6].Trim());
                    double      currentReal     = double.   Parse(data[7].Trim());
                    double      voltage         = 0.0;
                    try {       voltage         = double.   Parse(data[8].Trim()); }
                    catch {     voltage         = 0.0; }
                    double      resistance      = 0.0;
                    try {       resistance      = double.   Parse(data[9].Trim()); }
                    catch {     resistance      = 0.0;}
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
                            serialNumber,
                            model,
                            dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            connection))
                            fileDupe = 2;
                    
                    verificationDataList.Add(new SensorVerification
                    {
                        SerialNumber    = serialNumber,
                        Type            = type,
                        Model           = model,
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
                if (fileDupe == 2)
                    ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл верификации содержит дубликаты строк");
                else
                {
                    // Если некоторые сведения о характеризации имеются, то логируем предупреждение
                    if (fileDupe == 1)
                        ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Сведения о верификации уже есть в базе данных");

                    // Вставляем все данные верификации пакетом
                    try { databaseHelper.InsertVerificationDataBulk(verificationDataList, connection); }
                    catch { ErrorLogger.LogError(fileName, "ОШИБКА", "Не удалось выгрузить данные файла в базу данных"); }
                }
            }

            // Для возврата количества дубликатов
            bool skippedFile = fileDupe == 2;

            return skippedFile;
        }
    }
}
