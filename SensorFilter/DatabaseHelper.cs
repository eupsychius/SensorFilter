using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dapper;
using MessageBox = System.Windows.MessageBox;

namespace SensorFilter
{
    public class DatabaseHelper
    {
        // Метод создания ДБ
        public bool CreateDatabaseIfNotExists(string DbPath)
        {
            // Проверяем, существует ли ДБ
            if (!File.Exists(DbPath))
            {
                try
                {
                    // Создаём файл ДБ
                    SQLiteConnection.CreateFile(DbPath);

                    using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                    {
                        connection.Open();

                        // Создание таблиц
                        string createSensorTable = @"
                        CREATE TABLE Sensor (
	                    SensorId	        INTEGER,
	                    Channel	            INTEGER     NOT NULL,
	                    SerialNumber	    TEXT        NOT NULL,
	                    Type	            TEXT        NOT NULL,
	                    Model	            TEXT        NOT NULL,
	                    HasCharacterisation	INTEGER     DEFAULT 0,
	                    HasCoefficients	    INTEGER     DEFAULT 0,
	                    HasVerification	    INTEGER     DEFAULT 0,
	                    PRIMARY KEY(SensorId AUTOINCREMENT)
                        )";
                        
                        string createSensorDataTable = @"
                        CREATE TABLE IF NOT EXISTS SensorData (
                        DataId              INTEGER     PRIMARY KEY AUTOINCREMENT,
                        SerialNumber        TEXT        NOT NULL,
                        Model               TEXT        NOT NULL,
                        DateTime            TEXT        NOT NULL,
                        Temperature         REAL,
                        Range               INTEGER,
                        Pressure            REAL,
                        Voltage             REAL,
                        Resistance          REAL,
                        Deviation           REAL,
                        FOREIGN KEY (SerialNumber) REFERENCES Sensor(SerialNumber)
                        );";

                        string createSensorCoefficientsTable = @"
                        CREATE TABLE IF NOT EXISTS SensorCoefficients (
                        Id                  INTEGER     PRIMARY KEY AUTOINCREMENT,
                        SerialNumber        INTEGER     NOT NULL,
                        Model               TEXT        NOT NULL,
                        CoefficientIndex    INTEGER,
                        CoefficientValue    REAL,
                        CoefficientsDate	DATETIME    NOT NULL
                        );";

                        string createSensorVerificationTable = @"
                        CREATE TABLE IF NOT EXISTS SensorVerification (
                        Id                  INTEGER     PRIMARY KEY AUTOINCREMENT,
                        SerialNumber        INTEGER     NOT NULL,
                        Model               TEXT        NOT NULL,
                        DateTime            DATETIME    NOT NULL,
                        Temperature         REAL,
                        NPI                 REAL,
                        VPI                 REAL,
                        PressureGiven       REAL,
                        PressureReal        REAL,
                        CurrentGiven        REAL,
                        CurrentReal         REAL,
                        Voltage             REAL,
                        Resistance          REAL,
                        FOREIGN KEY(SerialNumber) REFERENCES Sensor(SerialNumber)
                        );";

                        using (SQLiteCommand command = new SQLiteCommand(createSensorTable,             connection)) { command.ExecuteNonQuery(); }
                        using (SQLiteCommand command = new SQLiteCommand(createSensorDataTable,         connection)) { command.ExecuteNonQuery(); }
                        using (SQLiteCommand command = new SQLiteCommand(createSensorCoefficientsTable, connection)) { command.ExecuteNonQuery(); }
                        using (SQLiteCommand command = new SQLiteCommand(createSensorVerificationTable, connection)) { command.ExecuteNonQuery(); }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Ошибка при создании базы данных: {ex.Message}", 
                        "Ошибка", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
                return false;
            }
            else
            {
                MessageBox.Show(
                    "База данных уже существует.", 
                    "Информация", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
                return true;
            }
        }

        // Метод динамического получения строки подключения
        private string GetConnStr()
        {
            return $"Data Source={Properties.DataBase.Default.DataBasePath};Version=3;";
        }

        // Получаем модели и типы по серийному номеру
        public (List<Sensor>, List<string>, List<string>) GetSensorBySerialNumber(string serialNumber)
        {
            try
            {
                using (var connection = new SQLiteConnection(GetConnStr()))
                {
                    // Получаем все данные по серийному номеру
                    string query = "SELECT * FROM Sensor WHERE SerialNumber = @SerialNumber";
                    var sensorList = connection.Query<Sensor>(query, new { SerialNumber = serialNumber }).AsList();

                    // Получаем список уникальных моделей
                    string modelQuery = "SELECT DISTINCT Model FROM Sensor WHERE SerialNumber = @SerialNumber";
                    var uniqueModels = connection.Query<string>(modelQuery, new { SerialNumber = serialNumber }).AsList();

                    // Получаем список уникальных типов
                    string typeQuery = "SELECT DISTINCT Type FROM Sensor WHERE SerialNumber = @SerialNumber";
                    var uniqueTypes = connection.Query<string>(typeQuery, new { SerialNumber = serialNumber }).AsList();

                    return (sensorList, uniqueTypes, uniqueModels);
                }
            }
            catch
            {
                MessageBox.Show(
                    "Произошла ошибка обмена связи с базой данных." +
                    "Возможно, вы подключены к устаревшей версии БД",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return (null, null, null);
            }
        }


        // Получаем характеризацию
        public List<SensorData> GetCharacterisationDataBySerialNumber(string serialNumber, string model)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                // Получаем все данные по серийному номеру
                string query = "SELECT * FROM SensorData WHERE SerialNumber = @SerialNumber AND Model = @Model";
                return connection.Query<SensorData>(query, new { SerialNumber = serialNumber, Model = model }).AsList();
            }
        }

        //
        public List<SensorVerification> GetVerificationDataBySerialNumber(string serialNumber, string model)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                string query = "SELECT * FROM SensorVerification WHERE SerialNumber = @SerialNumber AND Model = @Model";
                return connection.Query<SensorVerification>(query, new { SerialNumber = serialNumber, Model = model }).AsList();
            }
        }

        public List<SensorCoefficients> GetCoefficientsDataBySerialNumber(string serialNumber, string model)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                // Запрос для получения всех данных по конкретному SerialNumber
                string query = "SELECT * FROM SensorCoefficients WHERE SerialNumber = @SerialNumber AND Model = @Model";

                return connection.Query<SensorCoefficients>(query, new { SerialNumber = serialNumber, Model = model }).AsList();
            }
        }

        public Sensor GetSensorTypeBySerialNumber(string serialNumber, string model)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                // SQL-запрос для получения данных о датчике по его ID
                string query = "SELECT * FROM Sensor WHERE SerialNumber = @SerialNumber AND Model = @Model";

                return connection.QueryFirstOrDefault<Sensor>(query, new { SerialNumber = serialNumber, Model = model });
            }
        }

        public async Task<Dictionary<DateTime, List<SensorCoefficients>>> GetCoefficientsBySerialNumber(string serialNumber, string model)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                await connection.OpenAsync();
                string query = @"
                SELECT CoefficientIndex, CoefficientValue, CoefficientsDate 
                FROM SensorCoefficients 
                WHERE SerialNumber = @SerialNumber AND Model = @Model
                ORDER BY CoefficientsDate, CoefficientIndex";

                var result = await connection.QueryAsync<SensorCoefficients>(query, new { SerialNumber = serialNumber, Model = model });

                // Группируем по дате
                return result
                    .GroupBy(c => c.CoefficientsDate)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
        }

        public async Task<List<SensorCoefficients>> ExportCoefficientsBySerialNumber(string serialNumber)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                await connection.OpenAsync();
                string query = @"
                SELECT CoefficientIndex, CoefficientValue 
                FROM SensorCoefficients 
                WHERE SerialNumber = @SerialNumber
                ORDER BY CoefficientIndex";

                var coefficients = await connection.QueryAsync<SensorCoefficients>(query, new { SerialNumber = serialNumber });
                return coefficients.ToList();
            }
        }

        public int InsertSensorData(
            int channel,
            string serialNumber,
            string type,
            string model,
            SQLiteConnection connection)
        {
            // Проверка на наличие записи с таким серийным номером и моделью
            string checkQuery = @"
            SELECT SensorId FROM Sensor 
            WHERE SerialNumber = @serialNumber AND Model = @model";

            using (var checkCommand = new SQLiteCommand(checkQuery, connection))
            {
                checkCommand.Parameters.AddWithValue("@serialNumber", serialNumber);
                checkCommand.Parameters.AddWithValue("@model", model);

                var existingId = checkCommand.ExecuteScalar();

                // Если запись с таким серийным номером и моделью найдена, возвращаем её Id
                if (existingId != null)
                {
                    return Convert.ToInt32(existingId);
                }
            }
            
            // Если записи нет, вставляем новый датчик
            string insertQuery = @"
            INSERT INTO Sensor (Channel, SerialNumber, Type, Model)
            VALUES (@channel, @serialNumber, @type, @model); 
            SELECT last_insert_rowid();";

            using (var insertCommand = new SQLiteCommand(insertQuery, connection))
            {
                insertCommand.Parameters.AddWithValue("@channel",       channel);
                insertCommand.Parameters.AddWithValue("@serialNumber",  serialNumber);
                insertCommand.Parameters.AddWithValue("@type",          type);
                insertCommand.Parameters.AddWithValue("@model",         model);

                return Convert.ToInt32(insertCommand.ExecuteScalar()); // Получаем ID вставленного датчика
            }
        }


        public void InsertSensorDataBulk(IEnumerable<SensorData> sensorDataList, SQLiteConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                INSERT INTO SensorData 
                (SerialNumber, 
                Model, 
                DateTime, 
                Temperature, 
                Range, 
                Pressure, 
                Voltage, 
                Resistance, 
                Deviation) 
                VALUES 
                (@serialNumber, 
                @model, 
                @dateTime, 
                @temperature, 
                @range, 
                @pressure, 
                @voltage, 
                @resistance, 
                @deviation)";

                foreach (var characterisation in sensorDataList)
                {
                    command.Parameters.Clear(); // Очистка параметров для следующей вставки
                    command.Parameters.AddWithValue("@serialNumber",    characterisation.SerialNumber);
                    command.Parameters.AddWithValue("@model",           characterisation.Model);
                    command.Parameters.AddWithValue("@dateTime",        characterisation.DateTime);
                    command.Parameters.AddWithValue("@temperature",     characterisation.Temperature);
                    command.Parameters.AddWithValue("@range",           characterisation.Range);
                    command.Parameters.AddWithValue("@pressure",        characterisation.Pressure);
                    command.Parameters.AddWithValue("@voltage",         characterisation.Voltage);
                    command.Parameters.AddWithValue("@resistance",      characterisation.Resistance);
                    command.Parameters.AddWithValue("@deviation",       characterisation.Deviation);
                    command.ExecuteNonQuery();
                }

                var update = connection.CreateCommand();
                update.CommandText = @"
                UPDATE  Sensor
                SET     HasCharacterisation = '1'
                WHERE   SerialNumber = @serialNumber AND Model = @model AND Type = @type";
                update.Parameters.AddWithValue("@serialNumber", sensorDataList.First().SerialNumber);
                update.Parameters.AddWithValue("@model",        sensorDataList.First().Model);
                update.Parameters.AddWithValue("@type",         sensorDataList.First().Type);
                update.ExecuteNonQuery();

                transaction.Commit(); // Коммитим транзакцию для групповой вставки
            }
        }

        public void InsertSensorCoefficientsBulk(IEnumerable<SensorCoefficients> sensorCoefficientList, SQLiteConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                INSERT INTO SensorCoefficients 
                (SerialNumber,
                Model, 
                CoefficientIndex, 
                CoefficientValue,
                CoefficientsDate) 
                VALUES 
                (@serialNumber,
                @model,
                @coefficientIndex, 
                @coefficientValue,
                @coefficientsDate)";

                foreach (var coefficient in sensorCoefficientList)
                {
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@serialNumber",        coefficient.SerialNumber);
                    command.Parameters.AddWithValue("@type",                coefficient.Type);
                    command.Parameters.AddWithValue("@model",               coefficient.Model);
                    command.Parameters.AddWithValue("@coefficientIndex",    coefficient.CoefficientIndex);
                    command.Parameters.AddWithValue("@coefficientValue",    coefficient.CoefficientValue);
                    command.Parameters.AddWithValue("@coefficientsDate",    coefficient.CoefficientsDate);
                    command.ExecuteNonQuery();
                }

                var update = connection.CreateCommand();
                update.CommandText = @"
                UPDATE  Sensor
                SET     HasCoefficients = '1'
                WHERE   SerialNumber = @serialNumber AND Model = @model AND Type = @type";
                update.Parameters.AddWithValue("@serialNumber", sensorCoefficientList.First().SerialNumber);
                update.Parameters.AddWithValue("@model",        sensorCoefficientList.First().Model);
                update.Parameters.AddWithValue("@type",         sensorCoefficientList.First().Type);
                update.ExecuteNonQuery();

                transaction.Commit();
            }
        }

        public void InsertVerificationDataBulk(IEnumerable<SensorVerification> verificationDataList, SQLiteConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                INSERT INTO SensorVerification 
                (SerialNumber, 
                Model,      
                DateTime,   
                Temperature,    
                NPI, 
                VPI,    
                PressureGiven,  
                PressureReal,   
                CurrentGiven, 
                CurrentReal, 
                Voltage, 
                Resistance) 
                VALUES 
                (@serialNumber, 
                @model, 
                @dateTime, 
                @temperature, 
                @npi, 
                @vpi, 
                @pressureGiven, 
                @pressureReal, 
                @currentGiven, 
                @currentReal, 
                @voltage, 
                @resistance)";

                foreach (var verificationData in verificationDataList)
                {
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@serialNumber",    verificationData.SerialNumber   );
                    command.Parameters.AddWithValue("@model",           verificationData.Model          );
                    command.Parameters.AddWithValue("@dateTime",        verificationData.DateTime       );
                    command.Parameters.AddWithValue("@temperature",     verificationData.Temperature    );
                    command.Parameters.AddWithValue("@npi",             verificationData.NPI            );
                    command.Parameters.AddWithValue("@vpi",             verificationData.VPI            );
                    command.Parameters.AddWithValue("@pressureGiven",   verificationData.PressureGiven  );
                    command.Parameters.AddWithValue("@pressureReal",    verificationData.PressureReal   );
                    command.Parameters.AddWithValue("@currentGiven",    verificationData.CurrentGiven   );
                    command.Parameters.AddWithValue("@currentReal",     verificationData.CurrentReal    );
                    command.Parameters.AddWithValue("@voltage",         verificationData.Voltage        );
                    command.Parameters.AddWithValue("@resistance",      verificationData.Resistance     );
                    command.ExecuteNonQuery();
                }

                var update = connection.CreateCommand();
                update.CommandText = @"
                UPDATE  Sensor
                SET     HasVerification = '1'
                WHERE   SerialNumber = @serialNumber AND Model = @model AND Type = @type";
                update.Parameters.AddWithValue("@serialNumber", verificationDataList.First().SerialNumber   );
                update.Parameters.AddWithValue("@model",        verificationDataList.First().Model          );
                update.Parameters.AddWithValue("@type",         verificationDataList.First().Type           );
                update.ExecuteNonQuery();

                transaction.Commit();
            }
        }

        public bool CheckCharacterisationExists(string serialNumber, string type, string model, SQLiteConnection connection)
        {
            string query = @"
            SELECT COUNT(*) 
            FROM Sensor
            WHERE SerialNumber = @serialNumber AND Type = @type AND Model = @model AND HasCharacterisation = 1";

            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@serialNumber",    serialNumber);
                command.Parameters.AddWithValue("@type",            type);
                command.Parameters.AddWithValue("@model",           model);

                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }

        public bool CheckCoefficientExists(string serialNumber, string type, string model, SQLiteConnection connection)
        {
            string query = @"
            SELECT COUNT(*) 
            FROM Sensor
            WHERE SerialNumber = @serialNumber AND Type = @type AND Model = @model AND HasCoefficients = 1"; ;

            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@serialNumber",    serialNumber);
                command.Parameters.AddWithValue("@type",            type        );
                command.Parameters.AddWithValue("@model",           model       );

                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }

        public bool CheckVerificationExists(string serialNumber, string type, string model, SQLiteConnection connection)
        {
            string query = @"
            SELECT COUNT(*) 
            FROM Sensor
            WHERE SerialNumber = @serialNumber AND Type = @type AND Model = @model AND HasVerification = 1";

            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@serialNumber",    serialNumber);
                command.Parameters.AddWithValue("@type",            type    );
                command.Parameters.AddWithValue("@model",           model       );

                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }

        public List<string> SelectSerials(string selectedDate, string selectedType, string selectedModel)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                // Парсинг даты с выжимкой месяца и года
                DateTime date = DateTime.Parse(selectedDate);
                string selectedYearMonth = date.ToString("yyyy-MM");

                // Запрос на получение серийников
                string query = @"
                SELECT DISTINCT s.SerialNumber 
                FROM Sensor s
                JOIN SensorData sd ON s.SerialNumber = sd.SerialNumber
                WHERE strftime('%Y-%m', sd.DateTime) = @SelectedYearMonth
                AND s.Type = @SelectedType
                AND s.Model = @SelectedModel";

                // Исполнение запроса и возврат серийников
                return connection.Query<string>(query, new
                {
                    SelectedYearMonth   = selectedYearMonth,
                    SelectedType        = selectedType,
                    SelectedModel       = selectedModel
                }).AsList();
            }
        }

        public void DeleteSensorData(List<int> dataIds)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                connection.Open();
                string query = "DELETE FROM SensorData WHERE DataId IN @Ids";
                connection.Execute(query, new { Ids = dataIds });
            }
        }

        public void DeleteVerificationData(List<int> verificationIds)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                connection.Open();
                string query = "DELETE FROM SensorVerification WHERE Id IN @Ids";
                connection.Execute(query, new { Ids = verificationIds });
            }
        }

        public void DeleteCoefficientData(List<int> coefficientIds)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                connection.Open();
                string query = "DELETE FROM SensorCoefficients WHERE Id IN @Ids";
                connection.Execute(query, new { Ids = coefficientIds });
            }
        }

        public bool HasSensorRelatedData(string serialNumber, string model)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                connection.Open();

                // Проверяем наличие данных в SensorData
                string sensorDataQuery = "SELECT COUNT(*) FROM SensorData WHERE SerialNumber = @SerialNumber AND Model = @Model";
                long sensorDataCount = connection.ExecuteScalar<long>(sensorDataQuery, new { SerialNumber = serialNumber, Model = model });

                // Проверяем наличие данных в SensorVerification
                string verificationQuery = "SELECT COUNT(*) FROM SensorVerification WHERE SerialNumber = @SerialNumber AND Model = @Model";
                long verificationCount = connection.ExecuteScalar<long>(verificationQuery, new { SerialNumber = serialNumber, Model = model });

                // Проверяем наличие данных в SensorCoefficients
                string coefficientQuery = "SELECT COUNT(*) FROM SensorCoefficients WHERE SerialNumber = @SerialNumber AND Model = @Model";
                long coefficientCount = connection.ExecuteScalar<long>(coefficientQuery, new { SerialNumber = serialNumber, Model = model });

                return sensorDataCount > 0 || verificationCount > 0 || coefficientCount > 0;
            }
        }

        public bool SensorExists(string serialNumber, string type, string model, SQLiteConnection connection)
        {
            string query = @"
            SELECT COUNT(*)
            FROM Sensor
            WHERE SerialNumber = @SerialNumber AND Type = @Type AND Model = @Model";

            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@SerialNumber", serialNumber);
                command.Parameters.AddWithValue("@Type", type);
                command.Parameters.AddWithValue("@Model", model);

                long count = (long)command.ExecuteScalar();
                return count > 0; // Если count > 0, датчик уже существует
            }
        }

        public void DeleteSensorDataBySerialNumber(string serialNumber, string model)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                connection.Open();
                string query = "DELETE FROM SensorData WHERE SerialNumber = @SerialNumber AND Model = @Model";
                connection.Execute(query, new { SerialNumber = serialNumber, Model = model });
            }
        }

        public void DeleteVerificationDataBySerialNumber(string serialNumber, string model)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                connection.Open();
                string query = "DELETE FROM SensorVerification WHERE SerialNumber = @SerialNumber AND Model = @Model";
                connection.Execute(query, new { SerialNumber = serialNumber, Model = model });
            }
        }

        public void DeleteCoefficientDataBySerialNumber(string serialNumber, string model)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                connection.Open();
                string query = "DELETE FROM SensorCoefficients WHERE SerialNumber = @SerialNumber AND Model = @Model";
                connection.Execute(query, new { SerialNumber = serialNumber, Model = model });
            }
        }

        // Метод для удаления датчика
        public void DeleteSensor(string serialNumber, string model)
        {
            using (var connection = new SQLiteConnection(GetConnStr()))
            {
                connection.Open();
                string query = "DELETE FROM Sensor WHERE SerialNumber = @SerialNumber AND Model = @Model";
                connection.Execute(query, new { SerialNumber = serialNumber, Model = model });
            }
        }
    }
}
