using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bank
{
    class PersonalCabinet
    {
        private string userLogin = "";
        private readonly SQLiteCommand _sqlCmd = Program._sqlCmd;
        public static void Enter(string login, string password)
        {
            var cabinet = new PersonalCabinet
            {
                userLogin = login,
            };

            cabinet.ChooseNeedOperation();
        }

        private void ChooseNeedOperation()
        {
            Console.WriteLine("Выберите необходимую операция: (1 - действие со счётом, 2 - сменить код подтверждения, 3 - сменить пароль, 4 - узнать баланс)");
            switch (Console.ReadLine().Trim())
            {
                case "1":
                    ChooseActionWithBalance();
                    break;
                case "2":
                    ChangeSafeCode();
                    break;
                case "3":
                    ChangePassword();
                    break;
                case "4":
                    CheckBalance();
                    break;
                default:
                    Console.WriteLine("Некорректный ввод.");
                    ChooseNeedOperation();
                    break;
            }
        }

        private void ChooseActionWithBalance()
        {
            Console.WriteLine("Введите действие с балансом: (1 - перевести, 2 - пополнить, 0 - если хотите вернуться в меню)");
            switch (Console.ReadLine().Trim())
            {
                case "1":
                    TransferMoney();
                    break;
                case "2":
                    TopUpBalance();
                    break;
                case "0":
                    ChooseNeedOperation();
                    break;
                default:
                    Console.WriteLine("Некорректный ввод.");
                    ChooseActionWithBalance();
                    break;
            }
        }

        private void TransferMoney()
        {
            Console.WriteLine("Введите логин пользователя, которому вы хотите отправить перевод средств (0 - если хотите вернутся к меню):");
            string userTransferLogin = Console.ReadLine().Trim();
            
            if (userTransferLogin == "0")
            {
                ChooseActionWithBalance();
                return;
            }

            if (!Program.CheckForExistAccount(userTransferLogin))
            {
                Console.WriteLine($"Пользователь {userTransferLogin} не существует");
                TransferMoney();
                return;
            }

            Console.WriteLine("Введите сумму перевода:");
            if (int.TryParse(Console.ReadLine(), out int countMoney))
            {
                if (GetBalance() < countMoney)
                {
                    Console.WriteLine($"У вас не хватает средств на балансе. Ваш баланс - {GetBalance()}");
                    TransferMoney();
                    return;
                }
                
                _sqlCmd.CommandText = $"UPDATE users SET balance=balance - {countMoney} WHERE login='{userLogin}'";
                _sqlCmd.ExecuteNonQuery();

                _sqlCmd.CommandText = $"UPDATE users SET balance=balance + {countMoney} WHERE login='{userTransferLogin}'";
                _sqlCmd.ExecuteNonQuery();

                Console.WriteLine($"Перевод пользователю {userTransferLogin} успешно выполнен!");
                ChooseNeedOperation();
            }
            else
            {
                Console.WriteLine("Некорректная сумма перевода.");
                TransferMoney();
                return;
            }
        }

        private void TopUpBalance()
        {
            Console.WriteLine("Введите сумму пополнения:");
            string sumTopUp = Console.ReadLine().Trim();
            
            if (int.TryParse(sumTopUp, out int sumTopUpParsed))
            {
                _sqlCmd.CommandText = $"UPDATE users SET balance=balance + {sumTopUpParsed} WHERE login='{userLogin}'";
                _sqlCmd.ExecuteNonQuery();
                Console.WriteLine($"Ваш баланс успешно пополнен на {sumTopUpParsed} RUB!");
                ChooseNeedOperation();
            }
        }

        private void ChangeSafeCode()
        {
            Console.WriteLine("Введите старый секретый код (0 - если хотите вернуться к меню)");
            var oldCode = Console.ReadLine().Trim();
            if (oldCode == "0")
            {
                ChooseNeedOperation();
            }
            if (int.TryParse(oldCode, out int oldCodeParsed))
            {
                int trueOldCode = 0;

                _sqlCmd.CommandText = $"SELECT * FROM users WHERE login='{userLogin}'";
                _sqlCmd.ExecuteNonQuery();
                using (SQLiteDataReader reader = _sqlCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        trueOldCode = reader.GetInt32(2);
                    }
                }

                if (trueOldCode != oldCodeParsed)
                {
                    Console.WriteLine("Коды не совпадают!");
                    ChangeSafeCode();
                    return;
                }

                Console.WriteLine("Введите новый код:");
                var newCode = Console.ReadLine().Trim();
                if (int.TryParse(newCode, out int newCodeParsed))
                {
                    if (newCode.Length != 6)
                    {
                        Console.WriteLine("Секретный код должен быть равен 6 знакам!");
                        ChangeSafeCode();
                        return;
                    }

                    _sqlCmd.CommandText = $"UPDATE users SET safeCode={newCodeParsed} WHERE login='{userLogin}'";
                    _sqlCmd.ExecuteNonQuery();

                    Console.WriteLine("Секретный код был изменён успешно!");
                    ChooseNeedOperation();
                }
                else
                {
                    Console.WriteLine("Вы ввели некорректный код.");
                    ChangeSafeCode();
                    return;
                }
            }
            else
            {
                Console.WriteLine("Вы ввели некорректный код.");
                ChangeSafeCode();
                return;
            }
        }

        private void ChangePassword()
        {
            Console.WriteLine("Введите старый пароль (0 - если хотите вернуться к меню)");
            var oldPassword = Console.ReadLine().Trim();
            if (oldPassword == "0")
            {
                ChooseNeedOperation();
            }
            string trueOldPassword = "";

            _sqlCmd.CommandText = $"SELECT * FROM users WHERE login='{userLogin}'";
            _sqlCmd.ExecuteNonQuery();
            using (SQLiteDataReader reader = _sqlCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    trueOldPassword = reader.GetString(1);
                }
            }

            if (trueOldPassword != Program.EncodeToMD5(oldPassword))
            {
                Console.WriteLine($"Пароли не совпадают! {trueOldPassword} != {Program.EncodeToMD5(oldPassword)}");
                ChangePassword();
                return;
            }

            Console.WriteLine("Введите новый пароль:");
            var newPassword = Console.ReadLine().Trim();
            if (!Program.CheckPassword(newPassword))
            {
                Console.WriteLine("Пароль должен быть более 8 знаков!");
                ChangePassword();
                return;
            }

            _sqlCmd.CommandText = $"UPDATE users SET password='{Program.EncodeToMD5(newPassword)}' WHERE login={userLogin}";
            _sqlCmd.ExecuteNonQuery();

            Console.WriteLine("Пароль был изменён успешно!");
            ChooseNeedOperation();
        }
        private int GetBalance()
        {
            _sqlCmd.CommandText = $"SELECT * FROM users WHERE login='{userLogin}'";
            _sqlCmd.ExecuteNonQuery();

            int balance = 0;

            using (SQLiteDataReader reader = _sqlCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    balance = reader.GetInt32(3);
                }
            }
            return balance;
        }
        private void CheckBalance()
        {

            Console.WriteLine($"Ваш баланс - {GetBalance()} RUB");
            ChooseNeedOperation();
        }
    }
}
