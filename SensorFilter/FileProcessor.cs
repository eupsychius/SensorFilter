using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace SensorFilter
{
    public class FileProcessor
    {
        private        readonly DatabaseHelper  databaseHelper  = new();
        private static readonly SensorModels    sensorModels    = new();

        // Получаем строки файла на парсинг
        public bool ParseCharacterisationData(string[] lines, string dbPath, string fileName, string filePath)
        {
            /// Инт на проверку дубликатов
            /// 0 - Файл не дублирован
            /// 1 - Некоторые сведения уже имеются в ДБ
            /// 2 - Повторяющийся файл  
            int fileDupe = 0;

            // Данные парсинга датчика
            string  sensorInfo      = lines[1];
            var     sensorDetails   = sensorInfo.Split(';');

            string  serialNumber    =                                           sensorDetails[1].Split(':')[1].Trim();
            string  type            = DecodeString(sensorDetails.Length > 2 ?   sensorDetails[2].Split(':')[1].Trim() : "-");
            string  model           = DecodeString(sensorDetails.Length > 3 ?   sensorDetails[3].Split(':')[1].Trim() : "-");

            if (type == "-" || model == "-") ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл содержит неполные сведения о датчике");

            type = VerifyTypeAndModel(type, model, fileName);

            using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                connection.Open();
                int sensorId = DatabaseHelper.InsertSensorInfo(serialNumber, type, model, connection);

                var sensorCharacterisationList  = new List<SensorCharacterisation>();
                var sensorCoefficientList       = new List<SensorCoefficients>();

                bool    coefficientsSection = false;
                bool    formatWarned        = false;
                int     coefficientCount    = 0;
                DateTime date = DateTime.MinValue;

                for (int line = 5; line < lines.Length; line++)
                {
                    if (lines[line].Contains("Коэффициенты датчика"))
                    {
                        formatWarned        = false;
                        coefficientsSection = true;
                        coefficientCount    = int.Parse(lines[++line].Split(':')[1].Trim());
                        line++;
                    }

                    if (!coefficientsSection)
                    {
                        var data = lines[line].Split('|');

                        if (!lines[line].Contains("|")) continue;

                        DateTime dateTime;
                        double temperature;
                        int range;
                        double pressure;
                        double voltage;
                        double resistance;
                        double deviation;

                        try
                        {
                            dateTime = DateTime.Parse(data[0].Trim());
                            temperature = double.Parse(data[1].Trim());
                            range = int.Parse(data[2].Trim());
                            pressure = double.Parse(data[3].Trim());
                            voltage = double.Parse(data[4].Trim());
                            resistance = double.Parse(data[5].Trim());
                            try
                            { deviation = double.Parse(data[6].Trim()); }
                            catch
                            { deviation = 0.0; }
                        }
                        catch
                        {
                            if (!formatWarned) ErrorLogger.LogErrorAsync(fileName, "ОШИБКА", "Файл содержит строку характеризации неправильного формата");
                            formatWarned = true;
                            continue;
                        }

                        //Проверка на дефектность значений
                        if ((
                            (type == "ЭнИ-100" || type == "-") && (
                            voltage <= -1150 || voltage >= 1150 ||
                            resistance <= 0 || resistance >= 7200))
                            ||
                            (type == "ЭнИ-12" && (
                            voltage <= -290 || voltage >= 290 ||
                            resistance <= 0 || resistance >= 6800))
                            ||
                            (type == "ЭнИ-12М") && (
                            resistance <= 0 || resistance >= 5300))
                        {
                            ErrorLogger.LogErrorAsync(fileName, "БРАК", "Файл неудачной характеризации");

                            MoveToDefect(fileName, filePath);

                            return true;
                        }

                        sensorCharacterisationList.Add(new SensorCharacterisation
                        {
                            SensorId = sensorId,
                            DateTime = dateTime,
                            Temperature = temperature,
                            Range = range,
                            Pressure = pressure,
                            Voltage = voltage,
                            Resistance = resistance,
                            Deviation = deviation
                        });

                        date = dateTime;
                    }
                    else
                    {
                        if (Math.Abs((sensorCharacterisationList.Last().DateTime - sensorCharacterisationList.First().DateTime).TotalDays) > 7)
                        {
                            ErrorLogger.LogErrorAsync(fileName, "БРАК", "Файл содержит данные характеризации за разные даты");

                            MoveToDefect(fileName, filePath);

                            return true;
                        }

                        var coefficientData = lines[line].Split(':');

                        try
                        {
                            int coefficientIndex = int.Parse(coefficientData[0].Trim());
                            double coefficientValue = double.Parse(coefficientData[1].Trim());

                            sensorCoefficientList.Add(new SensorCoefficients
                            {
                                SensorId = sensorId,
                                CoefficientIndex = coefficientIndex,
                                CoefficientValue = coefficientValue,
                                CoefficientsDate = date
                            });
                        }
                        catch
                        {
                            if (!formatWarned) ErrorLogger.LogErrorAsync(fileName, "ОШИБКА", "Файл содержит строку коэффициентов неправильного формата");
                            formatWarned = true;
                            continue;
                        }
                    }
                }

                try
                {
                    if (databaseHelper.CheckCharacterisationExists(sensorId, connection)) fileDupe = 1;
                    fileDupe = databaseHelper.InsertSensorCharacterisationBulk(sensorCharacterisationList, connection, fileDupe != 0);
                    if (sensorCoefficientList.Count > 0 && fileDupe != 2)
                    {
                        fileDupe = databaseHelper.InsertSensorCoefficientsBulk(sensorCoefficientList, connection, fileDupe != 0);
                    }

                }
                catch (Exception ex) { ErrorLogger.LogErrorAsync(fileName, "ОШИБКА", ex.Message); }

                // Если словили дублирующуюся строку, то сведения не заносятся
                if (fileDupe == 2) ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл содержит дубликаты строк");
                else if (fileDupe == 1) ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Сведения о характеризации уже есть в базе данных");
            }

            // Для возврата количества пропущенных файлов
            return fileDupe == 2;
        }

        public bool ParseVerificationData(string[] lines, string dbPath, string fileName, string filePath)
        {
            /// Инт на проверку дубликатов
            /// 0 - Файл не дублирован
            /// 1 - Некоторые сведения уже имеются в ДБ
            /// 2 - Повторяющийся файл  
            int fileDupe = 0;

            var verificationDataList = new List<SensorVerification>(); // Коллекция для пакетной вставки данных верификации

            // Парсим информацию о датчике из второй строки
            string sensorInfo = lines[1];
            var sensorDetails = sensorInfo.Split(';');

            string serialNumber = sensorDetails[1].Split(':')[1].Trim();
            string type = DecodeString(sensorDetails.Length > 2 ? sensorDetails[2].Split(':')[1].Trim() : "-");
            string model = DecodeString(sensorDetails.Length > 3 ? sensorDetails[3].Split(':')[1].Trim() : "-");

            if (type == "-" || model == "-") ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл содержит неполные сведения о датчике");

            // Конвертим нестандартную модель в общую
            type = VerifyTypeAndModel(type, model, fileName);

            using (SQLiteConnection connection = new($"Data Source={dbPath};Version=3;"))
            {
                connection.Open();

                // Вставляем информацию о сенсоре
                int sensorId = DatabaseHelper.InsertSensorInfo(serialNumber, type, model, connection);

                // Проверяем наличие дубликатов по серийному номеру и дате
                if (databaseHelper.CheckVerificationExists(sensorId, connection)) fileDupe = 1;

                bool formatWarned = false;

                // Парсим данные верификации, начиная с 5 строки
                for (int line = 5; line < lines.Length; line++)
                {
                    // Отлов пустых строк
                    if (!lines[line].Contains("|")) continue;

                    var data = lines[line].Split('|');

                    DateTime dateTime;
                    double temperature;
                    double npi;
                    double vpi;
                    double pressureGiven;
                    double pressureReal;
                    double currentGiven;
                    double currentReal;
                    double voltage;
                    double resistance;

                    try
                    {
                        dateTime = DateTime.Parse(data[0].Trim());
                        temperature = double.Parse(data[1].Trim());
                        npi = double.Parse(data[2].Trim());
                        vpi = double.Parse(data[3].Trim());
                        pressureGiven = double.Parse(data[4].Trim());
                        pressureReal = double.Parse(data[5].Trim());
                        currentGiven = double.Parse(data[6].Trim());
                        currentReal = double.Parse(data[7].Trim());
                        try
                        { voltage = double.Parse(data[8].Trim()); }
                        catch
                        { voltage = 0.0; }
                        try
                        { resistance = double.Parse(data[9].Trim()); }
                        catch
                        { resistance = 0.0; }
                    }
                    catch
                    {
                        if (!formatWarned) ErrorLogger.LogErrorAsync(fileName, "ОШИБКА", "Файл содержит строку верификации неправильного формата");
                        formatWarned = true;
                        continue;
                    }

                    if (currentReal <= 3.81 || currentReal >= 20.47)
                    {
                        ErrorLogger.LogErrorAsync(fileName, "БРАК", "Файл неудачной верификации");

                        MoveToDefect(fileName, filePath);

                        return true;
                    }

                    verificationDataList.Add(new SensorVerification
                    {
                        SensorId = sensorId,
                        DateTime = dateTime,
                        Temperature = temperature,
                        NPI = npi,
                        VPI = vpi,
                        PressureGiven = pressureGiven,
                        PressureReal = pressureReal,
                        CurrentGiven = currentGiven,
                        CurrentReal = currentReal,
                        Voltage = voltage,
                        Resistance = resistance
                    });
                }

                if (Math.Abs((verificationDataList.Last().DateTime - verificationDataList.First().DateTime).TotalDays) > 7)
                {
                    ErrorLogger.LogErrorAsync(fileName, "БРАК", "Файл содержит данные верификации за разные даты");

                    MoveToDefect(fileName, filePath);

                    return true;
                }

                // Вставляем все данные верификации пакетом
                // Если словили дублирующуюся строку, то сведения не заносятся
                try
                {
                    fileDupe = databaseHelper.InsertVerificationBulk(verificationDataList, connection, fileDupe != 0);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogErrorAsync(fileName, "ОШИБКА", ex.Message);
                }

                if (fileDupe == 2) ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл верификации содержит дубликаты строк");
                else if (fileDupe == 1) ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Сведения о верификации уже есть в базе данных");
            }

            return fileDupe == 2;
        }

        private string VerifyTypeAndModel(string type, string model, string fileName)
        {
            // Конвертим нестандартную модель в общую
            if (type.Contains("ЭнИ-100") || type.Contains("ЭНИ-100")) type = "ЭнИ-100";
            if (type == "ЭНИ-12") type = "ЭнИ-12";
            if (type == "ЭНИ-12М") type = "ЭнИ-12М";

            if (model != "-" &&
                (((type == "ЭнИ-100") && !sensorModels.EnI100_Models().Contains(model)) ||
                ((type == "ЭнИ-12") && !sensorModels.EnI12_Models().Contains(model)) ||
                ((type == "ЭнИ-12М") && !sensorModels.EnI12M_Models().Contains(model))))
                ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", $"Файл содержит нестандартную модель - {model} ({type})");

            return type;
        }

        static string DecodeString(string rawData)
        {
            if (string.IsNullOrWhiteSpace(rawData) || rawData.Contains("\0"))
            {
                // Если модель содержит нулевые байты, обработать их
                byte[] blobData = Encoding.UTF8.GetBytes(rawData);
                // Попытка преобразования из blob
                try
                {
                    string result = Encoding.UTF8.GetString(blobData).Trim('\0');
                    if (result == "") result = "Ошибка";
                    return result;
                }
                catch
                {
                    return "Unknown";
                }
            }

            return rawData;
        }

        private void MoveToDefect(string fileName, string filePath)
        {
            // Получаем путь к директории "брак"
            string archiveDirectory = Path.GetDirectoryName(filePath);
            string defectDirectory = Path.Combine(archiveDirectory, "брак");

            // Создаем директорию, если она не существует
            if (!Directory.Exists(defectDirectory))
            {
                Directory.CreateDirectory(defectDirectory);
            }

            // Перемещаем файл в папку "брак"
            string targetPath = Path.Combine(defectDirectory, fileName);
            File.Move(filePath, targetPath);
        }
    }
}
