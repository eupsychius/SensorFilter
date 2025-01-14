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
                        SensorId            INTEGER     UNIQUE,
                        SerialNumber	    TEXT        NOT NULL,
                        Type	            TEXT        NOT NULL,
                        Model	            TEXT        NOT NULL,
                        HasCharacterisation	INTEGER     DEFAULT 0,
                        HasCoefficients	    INTEGER     DEFAULT 0,
                        HasVerification	    INTEGER     DEFAULT 0,
                        PRIMARY KEY(SensorId AUTOINCREMENT)
                        );";
                        
                        string createSensorCharacterisationTable = @"
                        CREATE TABLE SensorCharacterisation (
                        CharacterisationId	INTEGER     UNIQUE,
                        SensorId	        INTEGER     NOT NULL,
                        DateTime	        TEXT        NOT NULL,
                        Temperature	        REAL,
                        Range	            INTEGER,
                        Pressure	        REAL,
                        Voltage	            REAL,
                        Resistance	        REAL,
                        Deviation	        REAL,
                        PRIMARY KEY(CharacterisationId AUTOINCREMENT),
                        FOREIGN KEY(SensorId) REFERENCES Sensor(SensorId)
                        );";

                        string createSensorCoefficientsTable = @"
                        CREATE TABLE SensorCoefficients (
                        CoefficientId	    INTEGER     UNIQUE,
                        SensorId	        INTEGER     NOT NULL,
                        CoefficientIndex	INTEGER,
                        CoefficientValue	REAL,
                        CoefficientsDate	DATETIME    NOT NULL,
                        PRIMARY KEY(CoefficientId AUTOINCREMENT),
                        FOREIGN KEY(SensorId) REFERENCES Sensor(SensorId)
                        );";

                        string createSensorVerificationTable = @"
                        CREATE TABLE SensorVerification (
                        VerificationId	    INTEGER     UNIQUE,
                        SensorId	        INTEGER     NOT NULL,
                        DateTime	        DATETIME    NOT NULL,
                        Temperature	        REAL,
                        NPI	                REAL,
                        VPI	                REAL,
                        PressureGiven	    REAL,
                        PressureReal	    REAL,
                        CurrentGiven	    REAL,
                        CurrentReal	        REAL,
                        Voltage	            REAL,
                        Resistance	        REAL,
                        PRIMARY KEY(VerificationId AUTOINCREMENT),
                        FOREIGN KEY(SensorId) REFERENCES Sensor(SensorId)
                        );";

                        using (SQLiteCommand command = new(createSensorTable,                 connection)) { command.ExecuteNonQuery(); }
                        using (SQLiteCommand command = new(createSensorCharacterisationTable, connection)) { command.ExecuteNonQuery(); }
                        using (SQLiteCommand command = new(createSensorCoefficientsTable,     connection)) { command.ExecuteNonQuery(); }
                        using (SQLiteCommand command = new(createSensorVerificationTable,     connection)) { command.ExecuteNonQuery(); }
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
        private static string ConnStr => $"Data Source={Properties.DataBase.Default.DataBasePath};Version=3;";

        public static int? GetSensorId(string serialNumber, string type, string model)
        {
            using var connection = new SQLiteConnection(ConnStr);

            string query = "SELECT SensorId FROM Sensor WHERE SerialNumber = @SerialNumber AND Type = @Type AND Model = @Model";
            return connection.QueryFirstOrDefault<int?>(query, new { SerialNumber = serialNumber, Type = type, Model = model });
        }

        /* Получаем модели и типы по серийному номеру
         * Этот метод предполагает использование именно серийного номера для получения необходимых данных
         */
        public static (List<Sensor>, List<string>, List<string>) GetSensorBySerialNumber(string serialNumber)
        {
            try
            {
                using var connection = new SQLiteConnection(ConnStr);

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
        public List<SensorCharacterisation> GetCharacterisationData(int sensorId)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                // Получаем все данные по серийному номеру
                string query = "SELECT * FROM SensorCharacterisation WHERE SensorId = @SensorId";
                return connection.Query<SensorCharacterisation>(query, new { SensorId = sensorId }).AsList();
            }
        }

        //
        public List<SensorVerification> GetVerificationData(int sensorId)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                string query = "SELECT * FROM SensorVerification WHERE SensorId = @SensorId";
                return connection.Query<SensorVerification>(query, new { SensorId = sensorId }).AsList();
            }
        }

        public List<SensorCoefficients> GetCoefficientsData(int sensorId)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                // Запрос для получения всех данных по конкретному SerialNumber
                string query = "SELECT * FROM SensorCoefficients WHERE SensorId = @SensorId";

                return connection.Query<SensorCoefficients>(query, new { SensorId = sensorId }).AsList();
            }
        }

        public (string SerialNumber, string Type, string Model)? GetSensorInfo(int sensorId)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                string query = "SELECT SerialNumber, Type, Model FROM Sensor WHERE SensorId = @SensorId";

                return connection.QueryFirstOrDefault<(string SerialNumber, string Type, string Model)> (query, new { SensorId = sensorId });
            }
        }

        public async Task<Dictionary<DateTime, List<SensorCoefficients>>> GetCoefficients(int sensorId)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                await connection.OpenAsync();
                string query = @"
                SELECT CoefficientIndex, CoefficientValue, CoefficientsDate 
                FROM SensorCoefficients 
                WHERE SensorId = @SensorId
                ORDER BY CoefficientsDate, CoefficientIndex";

                var result = await connection.QueryAsync<SensorCoefficients>(query, new { SensorId = sensorId });

                // Группируем по дате
                return result
                    .GroupBy(c => c.CoefficientsDate)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
        }

        /*public async Task<List<SensorCoefficients>> ExportCoefficientsBySerialNumber(string serialNumber)
        {
            using (var connection = new SQLiteConnection(ConnStr))
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
        }*/

        public static int InsertSensorInfo(
            string              serialNumber,
            string              type,
            string              model,
            SQLiteConnection    connection)
        {
            // Проверка на наличие записи с таким серийным номером и моделью
            string checkQuery = @"
            SELECT SensorId FROM Sensor 
            WHERE SerialNumber = @serialNumber AND Model = @model AND Type = @type";

            using (var checkCommand = new SQLiteCommand(checkQuery, connection))
            {
                checkCommand.Parameters.AddWithValue("@serialNumber",   serialNumber);
                checkCommand.Parameters.AddWithValue("@model",          model       );
                checkCommand.Parameters.AddWithValue("@type",           type        );

                var existingId = checkCommand.ExecuteScalar();

                // Если запись с таким серийным номером и моделью найдена, возвращаем её Id
                if (existingId != null) return Convert.ToInt32(existingId);
            }
            
            // Если записи нет, вставляем новый датчик
            string insertQuery = @"
            INSERT INTO Sensor (SerialNumber, Type, Model)
            VALUES (@serialNumber, @type, @model); 
            SELECT last_insert_rowid();";

            using (var insertCommand = new SQLiteCommand(insertQuery, connection))
            {
                insertCommand.Parameters.AddWithValue("@serialNumber",  serialNumber);
                insertCommand.Parameters.AddWithValue("@type",          type        );
                insertCommand.Parameters.AddWithValue("@model",         model       );

                return Convert.ToInt32(insertCommand.ExecuteScalar()); // Получаем ID вставленного датчика
            }
        }

        public void InsertSensorCharacterisationBulk(IEnumerable<SensorCharacterisation> sensorCharacterisationList, SQLiteConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                INSERT INTO SensorCharacterisation 
                (SensorId, 
                DateTime, 
                Temperature, 
                Range, 
                Pressure, 
                Voltage, 
                Resistance, 
                Deviation) 
                VALUES 
                (   @sensorId, 
                    @dateTime, 
                    @temperature, 
                    @range, 
                    @pressure, 
                    @voltage, 
                    @resistance, 
                    @deviation)";

                foreach (var characterisation in sensorCharacterisationList)
                {
                    command.Parameters.Clear(); // Очистка параметров для следующей вставки
                    command.Parameters.AddWithValue("@sensorId",    characterisation.SensorId   );
                    command.Parameters.AddWithValue("@dateTime",    characterisation.DateTime   );
                    command.Parameters.AddWithValue("@temperature", characterisation.Temperature);
                    command.Parameters.AddWithValue("@range",       characterisation.Range      );
                    command.Parameters.AddWithValue("@pressure",    characterisation.Pressure   );
                    command.Parameters.AddWithValue("@voltage",     characterisation.Voltage    );
                    command.Parameters.AddWithValue("@resistance",  characterisation.Resistance );
                    command.Parameters.AddWithValue("@deviation",   characterisation.Deviation  );
                    command.ExecuteNonQuery();
                }

                var update = connection.CreateCommand();
                update.CommandText = @"
                UPDATE  Sensor
                SET     HasCharacterisation = '1'
                WHERE   SensorId = @sensorId";
                update.Parameters.AddWithValue("@sensorId", sensorCharacterisationList.First().SensorId );
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
                (
                SensorId,
                CoefficientIndex, 
                CoefficientValue,
                CoefficientsDate) 
                VALUES 
                (   @sensorId,
                    @coefficientIndex, 
                    @coefficientValue,
                    @coefficientsDate)";

                foreach (var coefficient in sensorCoefficientList)
                {
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@sensorId",            coefficient.SensorId        );       
                    command.Parameters.AddWithValue("@coefficientIndex",    coefficient.CoefficientIndex);
                    command.Parameters.AddWithValue("@coefficientValue",    coefficient.CoefficientValue);
                    command.Parameters.AddWithValue("@coefficientsDate",    coefficient.CoefficientsDate);
                    command.ExecuteNonQuery();
                }

                var update = connection.CreateCommand();
                update.CommandText = @"
                UPDATE  Sensor
                SET     HasCoefficients = '1'
                WHERE   SensorId = @sensorId";
                update.Parameters.AddWithValue("@sensorId", sensorCoefficientList.First().SensorId);
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
                (
                SensorId,
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
                (   @sensorId,
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
                    command.Parameters.AddWithValue("@sensorId",        verificationData.SensorId       );
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
                WHERE   SensorId = @sensorId";
                update.Parameters.AddWithValue("@sensorId", verificationDataList.First().SensorId);
                update.ExecuteNonQuery();

                transaction.Commit();
            }
        }

        public bool CheckCharacterisationExists(int sensorId, SQLiteConnection connection)
        {
            string query = @"
            SELECT COUNT(*) 
            FROM Sensor
            WHERE SensorId = @sensorId AND HasCharacterisation = 1";

            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@sensorId", sensorId);

                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }

        public bool CheckCharacterisationStringExists(int sensorId, string dateTime, SQLiteConnection connection)
        {
            string query = @"
            SELECT COUNT(*)
            FROM SensorCharacterisation
            WHERE SensorId = @sensorId
            AND DateTime   = @dateTime";

            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@sensorId", sensorId);
                command.Parameters.AddWithValue("@dateTime", dateTime);

                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }

        public bool CheckCoefficientExists(int sensorId, SQLiteConnection connection)
        {
            string query = @"
            SELECT COUNT(*) 
            FROM Sensor
            WHERE SensorId = @sensorId AND HasCoefficients = 1"; ;

            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@sensorId", sensorId);

                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }

        public bool CheckCoefficientStringExists(int sensorId, string coefficientIndex, string coefficientsDate, SQLiteConnection connection)
        {
            string query = @"
            SELECT  COUNT(*)
            FROM    SensorCoefficients
            WHERE   SensorId            = @sensorId
            AND     CoefficientIndex    = @coefficientIndex 
            AND     CoefficientsDate    = @CoefficientsDate";

            using (var command = new SQLiteCommand (query, connection))
            {
                command.Parameters.AddWithValue("@sensorId",            sensorId        );
                command.Parameters.AddWithValue("@coefficientIndex",    coefficientIndex);
                command.Parameters.AddWithValue("@CoefficientsDate",    coefficientsDate);

                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }

        public bool CheckVerificationExists(int sensorId, SQLiteConnection connection)
        {
            string query = @"
            SELECT COUNT(*) 
            FROM Sensor
            WHERE SensorId = @sensorId AND HasVerification = 1";

            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@sensorId", sensorId);

                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }

        public bool CheckVerificationStringExists(int sensorId, string model, string dateTime, SQLiteConnection connection)
        {
            string query = @"
            SELECT  COUNT(*) 
            FROM    SensorVerification
            WHERE   SensorId    = @sensorId
            AND     Model       = @model
            AND     DateTime    = @dateTime";

            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@sensorId",    sensorId);
                command.Parameters.AddWithValue("@model",       model   );
                command.Parameters.AddWithValue("@dateTime",    dateTime);

                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }

        // Возвращаем серийные номера для выпадающего списка
        public List<string> SelectSerials(string selectedDate, string selectedType, string selectedModel)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                // Парсинг даты с выжимкой месяца и года
                DateTime date = DateTime.Parse(selectedDate);
                string selectedYearMonth = date.ToString("yyyy-MM");

                // Обновленный запрос для получения серийных номеров
                string query = @"
                SELECT DISTINCT s.SerialNumber
                FROM Sensor s
                JOIN SensorCharacterisation sc ON s.SensorId = sc.SensorId
                WHERE strftime('%Y-%m', sc.DateTime) = @SelectedYearMonth
                AND s.Type  = @SelectedType
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

        public void DeleteSensorCharacterisationData(List<int> characterisationIds)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();
                string query = "DELETE FROM SensorCharacterisation WHERE CharacterisationId IN @Ids";
                connection.Execute(query, new { Ids = characterisationIds });
            }
        }

        public void DeleteVerificationData(List<int> verificationIds)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();
                string query = "DELETE FROM SensorVerification WHERE VerificationId IN @Ids";
                connection.Execute(query, new { Ids = verificationIds });
            }
        }

        public void DeleteCoefficientData(List<int> coefficientIds)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();
                string query = "DELETE FROM SensorCoefficients WHERE CoefficientId IN @Ids";
                connection.Execute(query, new { Ids = coefficientIds });
            }
        }

        public bool HasSensorRelatedData(int sensorId)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();

                // Проверяем наличие данных в SensorVerification
                string  sensorDataQuery     = "SELECT COUNT(*) FROM SensorCharacterisation WHERE SensorId = @SensorId";
                long    sensorDataCount     = connection.ExecuteScalar<long>(sensorDataQuery,   new { SensorId = sensorId });

                // Проверяем наличие данных в SensorVerification
                string  verificationQuery   = "SELECT COUNT(*) FROM SensorVerification WHERE SensorId = @SensorId";
                long    verificationCount   = connection.ExecuteScalar<long>(verificationQuery, new { SensorId = sensorId });

                // Проверяем наличие данных в SensorCoefficients
                string  coefficientQuery    = "SELECT COUNT(*) FROM SensorCoefficients WHERE SensorId = @SensorId";
                long    coefficientCount    = connection.ExecuteScalar<long>(coefficientQuery,  new { SensorId = sensorId });

                return sensorDataCount > 0 || verificationCount > 0 || coefficientCount > 0;
            }
        }

        public void DeleteCharacterisationData(int sensorId)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();
                string query = "DELETE FROM SensorCharacterisation WHERE SensorId = @SensorId";
                connection.Execute(query, new { SensorId = sensorId });
            }
        }

        public void DeleteVerificationData(int sensorId)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();
                string query = "DELETE FROM SensorVerification WHERE SensorId = @SensorId";
                connection.Execute(query, new { SensorId = sensorId });
            }
        }

        public void DeleteCoefficientData(int sensorId)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();
                string query = "DELETE FROM SensorCoefficients WHERE SensorId = @SensorId";
                connection.Execute(query, new { SensorId = sensorId });
            }
        }

        // Метод для удаления датчика
        public void DeleteSensor(int sensorId)
        {
            using (var connection = new SQLiteConnection(ConnStr))
            {
                connection.Open();
                string query = "DELETE FROM Sensor WHERE SensorId = @SensorId";
                connection.Execute(query, new { SensorId = sensorId });
            }
        }
    }
}
