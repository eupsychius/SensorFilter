using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace SensorFilter
{
    public class FileProcessor
    {
        private string          dataPath        = Properties.DataBase.Default.PendingDataPath;
        private DatabaseHelper  databaseHelper  = new();

        // Получаем строки файла на парсинг
        public (int, int) ParseCharacterisationData(string[] lines, string dbPath, string fileName)
        {
            // Для возврата количества дубликатов
            int skippedCharacterisation = 0;
            int skippedCoefficients     = 0;

            // Данные парсинга
            string  sensorInfo      = lines[1];
            var     sensorDetails   = sensorInfo.                   Split(';');
            int     channel         = int.Parse(sensorDetails[0].   Split(':')[1].Trim());
            string  serialNumber    = sensorDetails[1].             Split(':')[1].Trim();
            // Некоторые серийники идут без типа и модели
            string  type            = "-";
            string  model           = "-";
            try
            {
                type    = sensorDetails[2].Split(':')[1].Trim();
                model   = sensorDetails[3].Split(':')[1].Trim();
            }
            catch
            {
                ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл содержит неполные сведения о датчике");
            }
            // Конвертим нестандартную модель в общую
            if (type.Contains("ЭНИ-100") || type.Contains("ЭнИ-100"))
                type = "ЭНИ-100";

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

                DateTime date = new();

                // Булы для одноразовой отправки предупреждений
                bool charDupeWarned     = false;
                bool charNoDeviWarned   = false;
                bool coefDupeWarned     = false;

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
                        var data = lines[line].Split('|');
                        if (data.Length < 7) continue;
                        DateTime    dateTime    = DateTime. Parse(data[0].Trim());
                        double      temperature = double.   Parse(data[1].Trim());
                        int         range       = int.      Parse(data[2].Trim());
                        double      pressure    = double.   Parse(data[3].Trim());
                        double      voltage     = double.   Parse(data[4].Trim());
                        double      resistance  = double.   Parse(data[5].Trim());
                        double      deviation   = 0.0; // В некоторых файлах может отсутствовать
                        try
                        {
                            deviation           = double.   Parse(data[6].Trim());
                        }
                        catch (FormatException)
                        {
                            if (!charNoDeviWarned)
                            {
                                ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл не содержит данных по отклонению");
                                charNoDeviWarned = true;
                            }
                        }
                        date = dateTime;

                        // Проверяем наличие дубликатов по серийному номеру и дате
                        //if (databaseHelper.CheckCharacterisationExists(serialNumber, dateTime: dateTime, model, connection))
                        //{
                        //    skippedCharacterisation++; // Если уже существует, инкрементируем счетчик

                        //    if (!charDupeWarned)
                        //    {
                        //        ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Строки характеризации уже есть в базе данных и были пропущены");
                        //        charDupeWarned = true;
                        //    }

                        //    continue;
                        //}

                        // Собираем данные для пакетной вставки
                        sensorDataList.Add(new SensorData
                        {
                            SerialNumber    = serialNumber,
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

                        if (coefficientCount != actualCoefficientCount)
                            ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Заявленное кол-во коэффициентов не соответствует реальному");

                        for (int i = 0; i < actualCoefficientCount; i++, line++)
                        {
                            string[] coefficientData = lines[line].Split(':');

                            // Если данных не хватает, пропускаем эту строку
                            if (coefficientData.Length < 2) continue;

                            int     coefficientIndex;
                            double  coefficientValue;

                            if (!int.   TryParse(coefficientData[0].Trim(), out coefficientIndex) ||
                                !double.TryParse(coefficientData[1].Trim(), out coefficientValue))
                            {
                                // Пропуск некорректных строк, чтобы избежать исключений
                                continue;
                            }

                            DateTime coefficientsDate = date;

                            // Проверяем наличие дубликатов коэффициентов
                            //if (databaseHelper.CheckCoefficientExists(serialNumber, coefficientIndex, model, date, connection))
                            //{
                            //    skippedCoefficients++;

                            //    if (!coefDupeWarned)
                            //    {
                            //        ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Строки коэффициентов уже есть в базе данных и были пропущены");
                            //        coefDupeWarned = true;
                            //    }

                            //    continue;
                            //}

                            sensorCoefficientList.Add(new SensorCoefficients
                            {
                                SerialNumber        = serialNumber,
                                Model               = model,
                                CoefficientIndex    = coefficientIndex,
                                CoefficientValue    = coefficientValue,
                                CoefficientsDate    = coefficientsDate
                            });
                        }
                    }
                }

                // Вставляем все данные пакетом
                try
                {
                    databaseHelper.InsertSensorDataBulk(sensorDataList, connection);
                }
                catch
                {
                    ErrorLogger.LogError(fileName, "ОШИБКА", "Не удалось выгрузить данные характеризации в базу данных");
                }
                try
                {
                    databaseHelper.InsertSensorCoefficientsBulk(sensorCoefficientList, connection);
                }
                catch
                {
                    ErrorLogger.LogError(fileName, "ОШИБКА", "Не удалось выгрузить данные коэффициентов в базу данных");
                }
            }

            return (skippedCharacterisation, skippedCoefficients);
        }

        public int ParseVerificationData(string[] lines, string dbPath, string fileName)
        {
            int skippedVerification = 0;
            var verificationDataList = new List<SensorVerification>(); // Коллекция для пакетной вставки данных верификации

            // Парсим информацию о датчике из второй строки
            string  sensorInfo      = lines[1];
            var     sensorDetails   = sensorInfo.                   Split(';');
            int     channel         = int.Parse(sensorDetails[0].   Split(':')[1].Trim());
            string  serialNumber    = sensorDetails[1].             Split(':')[1].Trim();
            string  type            = "-";
            string  model           = "-";
            try
            {
                type    = sensorDetails[2].Split(':')[1].Trim();
                model   = sensorDetails[3].Split(':')[1].Trim();
            }
            catch
            {
                ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл содержит неполные сведения о датчике");
            }

            using (SQLiteConnection connection = new($"Data Source={dbPath};Version=3;"))
            {
                connection.Open();

                bool verDupeWarned      = false;
                bool verNoVoltWarned    = false;
                bool verNoResiWarned    = false;

                // Парсим данные верификации, начиная с 5 строки
                for (int line = 5; line < lines.Length; line++)
                {
                    if (string.IsNullOrEmpty(lines[line]))
                    {
                        ErrorLogger.LogErrorAsync(fileName, "ОТЛАДКА", "Файл содержит пустую строку");
                        continue;
                    }

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
                    try
                    {
                        voltage                 = double.   Parse(data[8].Trim());
                    }
                    catch
                    {
                        if (!verNoVoltWarned)
                        {
                            ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл не содержит данных по напряжению");
                            verNoVoltWarned = true;
                        }
                    }
                    double      resistance      = 0.0;  // В некоторых файлах может отсутствовать
                    try
                    {
                        resistance              = double.   Parse(data[9].Trim());
                    }
                    catch
                    {
                        if (!verNoResiWarned)
                        {
                            ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Файл не содержит данных по сопротивлению");
                            verNoResiWarned = true;
                        }
                    }
                    // Проверяем наличие дубликатов по серийному номеру и дате
                    //if (databaseHelper.CheckVerificationExists(serialNumber, dateTime, model, connection))
                    //{
                    //    skippedVerification++; // Если уже существует, инкрементируем счетчик

                    //    if (!verDupeWarned)
                    //    {
                    //        ErrorLogger.LogErrorAsync(fileName, "ПРЕДУПРЕЖДЕНИЕ", "Строки файла уже есть в базе данных и были пропущены");
                    //        verDupeWarned = true;
                    //    }

                    //    continue;
                    //}

                    verificationDataList.Add(new SensorVerification
                    {
                        SerialNumber    = serialNumber,
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

                // Вставляем все данные верификации пакетом
                try
                {
                    databaseHelper.InsertVerificationDataBulk(verificationDataList, connection);
                }
                catch
                {
                    ErrorLogger.LogError(fileName, "ОШИБКА", "Не удалось выгрузить данные файла в базу данных");
                }
            }

            return skippedVerification;
        }
    }
}
