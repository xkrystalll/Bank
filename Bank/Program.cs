using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Bank
{
    class Program
    {
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        public static double CurrentTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        public static SQLiteConnection _dbConn = new SQLiteConnection();
        public static SQLiteCommand _sqlCmd = new SQLiteCommand();
        private bool isConnected = false;

        public enum BalanceActions : int
        {
            Add = 1,
            Revoke = 2,
            Set = 4,
        }

        private void Register(string message, bool newRetry)
        {
            if (!newRetry)
            {
                Console.WriteLine(message);
            }

            Console.WriteLine("Введите логин (0 - если хотите вернуться назад):");
            string login = Console.ReadLine();
            if (login == "0")
            {
                Main(null);
                return;
            }

            if (login.Length < 3)
            {
                Console.WriteLine("Логин должен быть более 3 знаков!");
                Register(message, true);
                return;
            }
            if (CheckForExistAccount(login))
            {
                Console.WriteLine($"Логин {login} уже используется.");
                Register(message, true);
                return;
            }

            Console.WriteLine("Введите пароль:");
            string password = Console.ReadLine();
            if (!CheckPassword(password))
            {
                Console.WriteLine("Пароль должен быть более 8 знаков!");
                Register(message, true);
                return;
            }

            Console.WriteLine("Подтвердите пароль:");
            string confirmPassword = Console.ReadLine();

            if (password.Trim() != confirmPassword.Trim())
            {
                Console.WriteLine("Пароли не совпадают.");
                Register(message, true);
                return;
            }
            password = EncodeToMD5(password);
            WriteLogInData(login, password);
            var rand = new Random();
            int safeCode = rand.Next(100000, 999999);
            _sqlCmd.CommandText = $"INSERT INTO users (login, password, safeCode, balance, isBanned) VALUES ('{login}', '{password}', {safeCode}, 0, 0)";
            _sqlCmd.ExecuteNonQuery();
            Console.WriteLine($"Вы успешно зарегестрировались в системе! Ваш код восстановления - {safeCode}. Запомните его!");
            EnterData();
        }

        private bool TryGetOldData(out KeyValuePair<string, string> data)
        {
            string filePath = @"C:\Program Files (x86)\BankKrystalll\temp.txt";
            if (!Directory.Exists(@"C:\Program Files (x86)\BankKrystalll"))
            {
                Directory.CreateDirectory(@"C:\Program Files (x86)\BankKrystalll");
                data = new KeyValuePair<string, string>();
                return false;
            }
            if (!File.Exists(filePath))
            {
                File.Create(filePath);
                data = new KeyValuePair<string, string>();
                return false;
            }
            using (FileStream fileStream = File.OpenRead(filePath))
            {
                byte[] bytes = new byte[fileStream.Length];
                fileStream.Read(bytes, 0, bytes.Length);
                string decodedText = Encoding.Default.GetString(bytes);

                if (string.IsNullOrEmpty(decodedText))
                {
                    data = new KeyValuePair<string, string>();
                    return false;
                }
                //                                           LOGIN                          PASSWORD
                data = new KeyValuePair<string, string>(decodedText.Split(':')[0], decodedText.Split(':')[1]);
                return true;
            }
        }

        private void WriteLogInData(string login, string password)
        {
            try
            {
                Directory.CreateDirectory(@"C:\Program Files (x86)\BankKrystalll");
                /*File.Create(@"C:\Program Files (x86)\BankKrystalll\temp.txt");*/
            } catch { return; }

            using (FileStream fstream = new FileStream(@"C:\Program Files (x86)\BankKrystalll\temp.txt", FileMode.OpenOrCreate))
            {
                byte[] array = Encoding.Default.GetBytes(login + ":" + password);
                fstream.Write(array, 0, array.Length);
            }
        }

        public static bool CheckForExistAccount(string login)
        {
            _sqlCmd.CommandText = $"UPDATE users SET (login) = ('{login}') WHERE login='{login}'";
            int i = _sqlCmd.ExecuteNonQuery();
            if (i <= 0)
            {
                return false;
            }
            return true;
        }

        private void EnterData()
        {
            Console.WriteLine("Введите логин:");
            string login = Console.ReadLine();

            Console.WriteLine("Введите пароль:");
            string password = EncodeToMD5(Console.ReadLine());

            LogInAction(login, password);

        }
        
        public static bool CheckPassword(string password)
        {
            if (password.Length < 8)
            {
                return false;
            }
            return true;
        }

        private void LogInAction(string login, string password, bool needConfirmCode = false)
        {
            if (!CheckForExistAccount(login))
            {
                Console.WriteLine("Аккаунт не существует или вы ввели неверный пароль.");
                EnterData(); 
                return;
            }

            int needCode = 0;
            int isBanned = 0;
            string passwordFromDB = "";

            _sqlCmd.CommandText = $"SELECT * FROM users WHERE login='{login}'";
            _sqlCmd.ExecuteNonQuery();
            using (SQLiteDataReader reader = _sqlCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    needCode = reader.GetInt32(2);
                    isBanned = reader.GetInt32(4);
                    passwordFromDB = reader.GetString(1);
                }
            }

            if (isBanned == 1)
            {
                Console.WriteLine("Данный аккаунт был заблокирован на неопределённый промежуток времени.");
                Main(null);
                return;
            }
            if (password != passwordFromDB)
            {
                Console.WriteLine($"Аккаунт не существует или вы ввели неверный пароль. {password} != {passwordFromDB}");
                Main(null);
                return;
            }
            if (needConfirmCode)
            {
                Console.WriteLine("Введите Ваш код подтверждения, указанный при регистрации:");
                if (int.TryParse(Console.ReadLine(), out int enteredCode))
                {
                    if (enteredCode != needCode)
                    {

                        Console.WriteLine($"Введён неверный код подтверждения.");
                        Main(null);
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Вы успешно зашли в систему");
                        PersonalCabinet.Enter(login, password);
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Введён неверный код подтверждения.");
                    Main(null);
                    return;
                }
            }
            Console.WriteLine($"Вы успешно зашли в систему");
            PersonalCabinet.Enter(login, password);
        }

        public static string EncodeToMD5(string str)
        {
            var md5Encoder = MD5.Create();
            return Convert.ToBase64String(md5Encoder.ComputeHash(Encoding.UTF8.GetBytes(str)));
        }

        private void LogInByOldData(bool needConfirmCode)
        {
            var data = new KeyValuePair<string, string>();
            if (!TryGetOldData(out data))
            {
                Console.WriteLine("Не удалось войти в аккаунт по старым данным.");
                Main(new string[] { });
                return;
            }
            LogInAction(data.Key, data.Value, needConfirmCode);
        }

        private void TryConnectToSQL()
        {
            if (!File.Exists(@"users.sqlite"))
                File.Create(@"users.sqlite");
            try
            {
                _dbConn = new SQLiteConnection(string.Format(@"Data Source={0};Version=3;New=False;Compress=True;", @"users.sqlite"));

                _dbConn.Open();
                _sqlCmd.Connection = _dbConn;

                _sqlCmd.CommandText = "CREATE TABLE IF NOT EXISTS Users (login TEXT, password TEXT, safeCode INT, balance INT, isBanned INT)";
                _sqlCmd.ExecuteNonQuery();

                isConnected = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
        }

        static void Main(string[] args)
        {
            Program program = new Program();

            if (!program.isConnected)
            {
                program.TryConnectToSQL();
            }

            Console.WriteLine("Выберите действие (1 - войти в систему, 2 - регистрация, 3 - попробовать войти по старым данным):");
            string action = Console.ReadLine();
            switch (action.Trim())
            {
                case "1":
                    program.EnterData();
                    break;
                case "2":
                    program.Register("Зарегестрируйся в нашем банке прямо сейчас!", false);
                    break;
                case "3":
                    program.LogInByOldData(true);
                    break;
                default:
                    Console.WriteLine("Некорректный ввод.");
                    Main(args);
                    break;
            }
        }
    }
}
