using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using MySql.Data.MySqlClient;

namespace DbMaintenance
{
    class Program
    {
        static void Main(string[] args)
        {
            var db = new DbConnect();
            db.Backup();
            Console.WriteLine("\nDB size before: " + db.GetDbSize());
            db.ClearTransients();
            Console.WriteLine("\nDB size after: " + db.GetDbSize());
            db.Backup();
        }
    }

    class DbConnect
    {
        private MySqlConnection _connection;
        private string _server;
        private string _database;
        private string _uid;
        private string _password;
        private int _port;
        private string _connectionString;
        private string _backupDir;
        //private string _mysqldumpPath;

        //Constructor
        public DbConnect()
        {
            Initialize();
        }

        //Initialize values
        private void Initialize()
        {
            _backupDir = @"D:\home\db-backup";
            _server = ConfigurationManager.AppSettings["server"];
            _database = ConfigurationManager.AppSettings["database"];
            _uid = ConfigurationManager.AppSettings["user"];
            _password = ConfigurationManager.AppSettings["password"];
            _port = int.Parse(ConfigurationManager.AppSettings["port"]);
            _connectionString = $"Database={_database};Data Source={_server};Port={_port};User Id={_uid};Password={_password};SslMode=Required;";
            _connection = new MySqlConnection(_connectionString);
        }

        //open connection to database
        private bool OpenConnection()
        {
            try
            {
                _connection.Open();
                return true;
            }
            catch (MySqlException ex)
            {
                //When handling errors, you can your application's response based on the error number.
                //The two most common error numbers when connecting are as follows:
                //0: Cannot connect to server.
                //1045: Invalid user name and/or password.
                switch (ex.Number)
                {
                    case 0:
                        Console.WriteLine("Cannot connect to server. Contact administrator");
                        break;

                    case 1045:
                        Console.WriteLine("Invalid username/password, please try again");
                        break;
                }
                return false;
            }
        }

        //Close connection
        private bool CloseConnection()
        {
            try
            {
                _connection.Close();
                return true;
            }
            catch (MySqlException ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        //Clear transients
        public void ClearTransients()
        {
            Console.WriteLine("\nClearTransients...");

            string query = "DELETE FROM wp_options WHERE option_name LIKE ('%\\_transient\\_%');";

            //open connection
            if (OpenConnection())
            {
                //create command and assign the query and connection from the constructor
                var cmd = new MySqlCommand(query, _connection);

                //Execute command
                var result = cmd.ExecuteNonQuery();
                Console.WriteLine("Cleard Transients: " + result);

                //close connection
                CloseConnection();
            }
        }

        //Backup
        public void Backup()
        {
            Console.WriteLine("\nBackup...");
            try
            {
                var now = DateTime.Now;

                if (!Directory.Exists(_backupDir)) Directory.CreateDirectory(_backupDir);

                //Save file with the current date as a filename
                var path = _backupDir + "\\" + _database +
                    now.Year + "-" +
                    now.Month.ToString("00") + "-" +
                    now.Day.ToString("00") + "-" +
                    now.Hour.ToString("00") + "-" +
                    now.Minute.ToString("00") + "-" +
                    now.Second.ToString("00") + "-" +
                    now.Millisecond + ".sql";

                var file = new StreamWriter(path);

                string arguments = string.Format(@"-u{0} -p{1} -h{2} --port {3} {4}", _uid, _password, _server, _port, _database);

                Console.WriteLine($"Command: mysqldump {arguments}");

                var psi = new ProcessStartInfo
                {
                    FileName = "mysqldump",
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    Arguments = arguments,
                    UseShellExecute = false
                };

                var process = Process.Start(psi);

                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    file.WriteLine(output);
                    process.WaitForExit();
                    process.Close();
                }
                else
                {
                    throw new IOException("Cannot start a process...");
                }
                
                file.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error , unable to backup!");
                Console.WriteLine(ex.Message + ": " + ex.StackTrace);
            }
        }

        // Get Db size
        public string GetDbSize()
        {
            string query = "SELECT SUM(round(((data_length + index_length) / 1024 / 1024), 2)) 'Size in MB' " +
                "FROM information_schema.TABLES " +
                "WHERE table_schema = '" + _database + "' " +
                "ORDER BY (data_length + index_length) DESC;";

            //Open connection
            if (OpenConnection())
            {
                //Create Command
                var cmd = new MySqlCommand(query, _connection);
                //Create a data reader and Execute the command
                MySqlDataReader dataReader = cmd.ExecuteReader();

                //Read the data and store them
                dataReader.Read();
                var result = dataReader["Size in MB"] + " MB";

                //close Data Reader
                dataReader.Close();

                //close Connection
                CloseConnection();

                //return list to be displayed
                return result;
            }
            
            return "ERROR";
        }
    }
}
