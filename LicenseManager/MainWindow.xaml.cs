using LicenseManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace LicenseManager
{
    public partial class MainWindow : Window
    {
        private const string DbFile = @"T:\01-Phong-Ban-XN\03-XN TK Ha Tang\05-Tran Van Thoai\15.Project\License App\LicenseData.db";
        private static string ConnectionString => $"Data Source={DbFile};Version=3;";

        private ObservableCollection<OnlineUserInfo> users = new ObservableCollection<OnlineUserInfo>();

        public MainWindow()
        {
            InitializeComponent();
            gcLicenses.ItemsSource = users;

            EnsureDatabase();
            LoadAllUsersSmart();

            DispatcherTimer reloadTimer = new DispatcherTimer();
            reloadTimer.Interval = TimeSpan.FromSeconds(2);
            reloadTimer.Tick += (s, e) =>
            {
                LoadAllUsersSmart();
                UpdateOnlineStatus();
            };
            reloadTimer.Start();
        }

        private static void EnsureDatabase()
        {
            if (!System.IO.File.Exists(DbFile))
                SQLiteConnection.CreateFile(DbFile);

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS UserOnline (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ActiveUser TEXT,
                        MacAddress TEXT,
                        MachineName TEXT,
                        OpenTime DATETIME
                    );
                    CREATE TABLE IF NOT EXISTS Blocked (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ActiveUser TEXT,
                        MacAddress TEXT,
                        MachineName TEXT
                    );
                    CREATE TABLE IF NOT EXISTS MasterUser (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ActiveUser TEXT,
                        MacAddress TEXT,
                        MachineName TEXT
                    );
                ";
                cmd.ExecuteNonQuery();
            }
        }

        private string GetUserKey(OnlineUserInfo info)
        {
            return $"{info.ActiveUser}|{info.MacAddress}|{info.MachineName}";
        }

        /// <summary>
        /// Load tất cả user từng xuất hiện (master), trạng thái online/offline, block.
        /// Đồng bộ collection users mà KHÔNG dùng Clear (update/add/remove từng phần tử).
        /// </summary>
        private void LoadAllUsersSmart()
        {
            // 1. Lấy danh sách mới từ database như cũ
            var newUsers = new List<OnlineUserInfo>();
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
SELECT
  mu.ActiveUser,
  mu.MacAddress,
  mu.MachineName,
  uo.OpenTime,
  CASE
    WHEN uo.OpenTime IS NOT NULL AND (julianday('now') - julianday(uo.OpenTime)) < (35.0/86400.0) THEN 1
    ELSE 0
  END AS IsOnline,
  CASE
    WHEN EXISTS (
      SELECT 1 FROM Blocked b
      WHERE b.ActiveUser = mu.ActiveUser AND b.MacAddress = mu.MacAddress AND b.MachineName = mu.MachineName
    ) THEN 1 ELSE 0 END AS IsBlocked
FROM MasterUser mu
LEFT JOIN UserOnline uo ON
  mu.ActiveUser = uo.ActiveUser AND
  mu.MacAddress = uo.MacAddress AND
  mu.MachineName = uo.MachineName
ORDER BY mu.Id
";
                using (var reader = cmd.ExecuteReader())
                {
                    int stt = 1;
                    while (reader.Read())
                    {
                        var info = new OnlineUserInfo
                        {
                            STT = stt++,
                            ActiveUser = reader["ActiveUser"].ToString(),
                            MacAddress = reader["MacAddress"].ToString(),
                            MachineName = reader["MachineName"].ToString(),
                            OpenTime = reader["OpenTime"] != DBNull.Value ? Convert.ToDateTime(reader["OpenTime"]) : DateTime.MinValue,
                            IsOnline = reader["IsOnline"] != DBNull.Value && Convert.ToInt32(reader["IsOnline"]) == 1,
                            IsBlocked = reader["IsBlocked"] != DBNull.Value && Convert.ToInt32(reader["IsBlocked"]) == 1,
                        };
                        newUsers.Add(info);
                    }
                }
            }

            // 2. Đồng bộ users (ObservableCollection) với newUsers mà KHÔNG dùng Clear
            // a. Update hoặc add mới
            foreach (var u in newUsers)
            {
                var exist = users.FirstOrDefault(x => GetUserKey(x) == GetUserKey(u));
                if (exist == null)
                    users.Add(u);
                else
                {
                    exist.STT = u.STT;
                    exist.OpenTime = u.OpenTime;
                    exist.IsOnline = u.IsOnline;
                    exist.IsBlocked = u.IsBlocked;
                    // Nếu có thêm thuộc tính khác hãy cập nhật ở đây
                }
            }
            // b. Xóa những user không còn trong danh sách mới
            for (int i = users.Count - 1; i >= 0; i--)
            {
                var key = GetUserKey(users[i]);
                if (!newUsers.Any(u => GetUserKey(u) == key))
                    users.RemoveAt(i);
            }
        }

        private static bool IsUserBlocked(string user, string mac, string machineName)
        {
            EnsureDatabase();
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT 1 FROM Blocked WHERE ActiveUser=@user AND MacAddress=@mac AND MachineName=@pc";
                cmd.Parameters.AddWithValue("@user", user);
                cmd.Parameters.AddWithValue("@mac", mac);
                cmd.Parameters.AddWithValue("@pc", machineName);
                var res = cmd.ExecuteScalar();
                return res != null;
            }
        }

        /// <summary>
        /// Nếu user offline, trạng thái sẽ được cập nhật realtime nhờ IsOnline
        /// </summary>
        private void UpdateOnlineStatus()
        {
            foreach (var u in users)
            {
                bool isOnline = (DateTime.Now - u.OpenTime).TotalSeconds <= 35;
                if (u.IsOnline != isOnline)
                    u.IsOnline = isOnline;
            }
        }

        private void btnBlock_Click(object sender, RoutedEventArgs e)
        {
            var selected = gcLicenses.SelectedItem as OnlineUserInfo;
            if (selected == null)
            {
                MessageBox.Show("Hãy chọn tài khoản cần block.");
                return;
            }
            if (IsUserBlocked(selected.ActiveUser, selected.MacAddress, selected.MachineName))
            {
                MessageBox.Show("Tài khoản này đã bị block.");
                return;
            }
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Blocked (ActiveUser, MacAddress, MachineName) VALUES (@user, @mac, @pc)";
                cmd.Parameters.AddWithValue("@user", selected.ActiveUser);
                cmd.Parameters.AddWithValue("@mac", selected.MacAddress);
                cmd.Parameters.AddWithValue("@pc", selected.MachineName);
                cmd.ExecuteNonQuery();
            }
            LoadAllUsersSmart();
            MessageBox.Show($"Đã block tài khoản: {selected.ActiveUser}\nMAC: {selected.MacAddress}\nPC: {selected.MachineName}");
        }

        private void btnUnblock_Click(object sender, RoutedEventArgs e)
        {
            var selected = gcLicenses.SelectedItem as OnlineUserInfo;
            if (selected == null)
            {
                MessageBox.Show("Hãy chọn tài khoản cần unblock.");
                return;
            }
            if (!IsUserBlocked(selected.ActiveUser, selected.MacAddress, selected.MachineName))
            {
                MessageBox.Show("Tài khoản này chưa bị block.");
                return;
            }
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"DELETE FROM Blocked WHERE ActiveUser=@user AND MacAddress=@mac AND MachineName=@pc";
                cmd.Parameters.AddWithValue("@user", selected.ActiveUser);
                cmd.Parameters.AddWithValue("@mac", selected.MacAddress);
                cmd.Parameters.AddWithValue("@pc", selected.MachineName);
                cmd.ExecuteNonQuery();
            }
            LoadAllUsersSmart();
            MessageBox.Show($"Đã bỏ block tài khoản: {selected.ActiveUser}\nMAC: {selected.MacAddress}\nPC: {selected.MachineName}");
        }

        private void btnShowBlockList_Click(object sender, RoutedEventArgs e)
        {
            EnsureDatabase();
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT * FROM Blocked";
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        MessageBox.Show("Không có tài khoản nào đang bị block.", "Block List");
                        return;
                    }
                    var msg = "Các tài khoản đang bị block:\n";
                    while (reader.Read())
                    {
                        msg += $"- {reader["ActiveUser"]}";
                        if (reader["MacAddress"] != DBNull.Value) msg += $" | MAC: {reader["MacAddress"]}";
                        if (reader["MachineName"] != DBNull.Value) msg += $" | PC: {reader["MachineName"]}";
                        msg += "\n";
                    }
                    MessageBox.Show(msg, "Block List");
                }
            }
        }

        private void btnUnblockAll_Click(object sender, RoutedEventArgs e)
        {
            EnsureDatabase();
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"DELETE FROM Blocked";
                int count = cmd.ExecuteNonQuery();
                LoadAllUsersSmart();
                if (count == 0)
                    MessageBox.Show("Không có tài khoản nào để unblock.", "Unblock All");
                else
                    MessageBox.Show("Đã bỏ block toàn bộ tài khoản.");
            }
        }
    }
}