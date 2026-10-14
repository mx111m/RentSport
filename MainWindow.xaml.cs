using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace RentSport
{
    public partial class MainWindow : Window
    {
        private Db db;
        private List<Client> allClients = new List<Client>();
        private List<Item> allItems = new List<Item>();
        private List<Rental> allHistory = new List<Rental>();
        private bool loaded;
        private bool changingPhone;

        public MainWindow()
        {
            InitializeComponent();
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rent.db");
            db = new Db(path);
            RentStartDate.SelectedDate = DateTime.Today;
            RentEndDate.SelectedDate = DateTime.Today.AddDays(1);
            LoadData();
            loaded = true;
        }

        private void LoadData()
        {
            allClients.Clear();
            foreach (string[] row in db.Query("SELECT Id,Name,Phone FROM Clients ORDER BY Name"))
                allClients.Add(new Client { Id = int.Parse(row[0]), Name = row[1], Phone = FormatSavedPhone(row[2]) });
            RentClient.ItemsSource = null;
            RentClient.ItemsSource = new List<Client>(allClients);
            ShowClients();

            allItems.Clear();
            foreach (string[] row in db.Query("SELECT Id,Name,Category,Quantity,RepairCount,WrittenOff FROM Items ORDER BY Name"))
            {
                int id = int.Parse(row[0]);
                int qty = int.Parse(row[3]);
                int repair = int.Parse(row[4]);
                int writtenOff = int.Parse(row[5]);
                int rented = GetRented(id);
                int free = qty - repair - rented - writtenOff;
                string status = writtenOff >= qty ? "Списаний" : repair >= qty - writtenOff ? "На ремонті" : repair > 0 ? "Частково на ремонті" : free <= 0 ? "Немає вільних" : "Доступний";
                allItems.Add(new Item { Id = id, Name = row[1], Category = row[2], Quantity = qty, RepairCount = repair, WrittenOff = writtenOff, Rented = rented, Available = free, Status = status, Label = row[1] + " — доступно " + free });
            }
            ShowItems();
            int totalCount = 0;
            int freeCount = 0;
            foreach (Item item in allItems)
            {
                totalCount += item.Quantity - item.WrittenOff;
                freeCount += item.Available;
            }
            List<Item> freeItems = new List<Item>();
            foreach (Item item in allItems) if (item.Quantity - item.RepairCount - item.WrittenOff > 0) freeItems.Add(item);
            RentItem.ItemsSource = freeItems;

            allHistory.Clear();
            foreach (string[] row in db.Query("SELECT Rentals.Id,Clients.Name,Items.Name,Rentals.Quantity,Rentals.StartDate,Rentals.EndDate,IFNULL(Rentals.ReturnedAt,''),Rentals.Returned FROM Rentals JOIN Clients ON Clients.Id=Rentals.ClientId JOIN Items ON Items.Id=Rentals.ItemId ORDER BY Rentals.Id DESC"))
            {
                string state = row[7] == "1" ? "Повернено" : row[4].CompareTo(DateTime.Today.ToString("yyyy-MM-dd")) > 0 ? "Запланована" : "Активна";
                allHistory.Add(new Rental { Id = int.Parse(row[0]), Client = row[1], Item = row[2], Quantity = int.Parse(row[3]), StartDate = row[4], EndDate = row[5], ReturnedAt = row[6], State = state });
            }
            ShowHistory();
            AllCount.Text = totalCount.ToString();
            FreeCount.Text = freeCount.ToString();
            int active = int.Parse(db.Query("SELECT COUNT(*) FROM Rentals WHERE Returned=0 AND StartDate<=" + Db.Q(DateTime.Today.ToString("yyyy-MM-dd")))[0][0]);
            RentCount.Text = active.ToString();
            RentGrid.ItemsSource = null;
            List<Rental> current = new List<Rental>();
            foreach (Rental rental in allHistory) if (rental.State == "Активна") current.Add(rental);
            RentGrid.ItemsSource = current;
        }

        private int GetRented(int itemId)
        {
            return int.Parse(db.Query("SELECT IFNULL(SUM(Quantity),0) FROM Rentals WHERE ItemId=" + itemId + " AND Returned=0 AND StartDate<=" + Db.Q(DateTime.Today.ToString("yyyy-MM-dd")))[0][0]);
        }

        private int GetReserved(int itemId)
        {
            return int.Parse(db.Query("SELECT IFNULL(SUM(Quantity),0) FROM Rentals WHERE ItemId=" + itemId + " AND Returned=0")[0][0]);
        }

        private int GetAvailable(int itemId, DateTime end)
        {
            string[] row = db.Query("SELECT Quantity,RepairCount,WrittenOff FROM Items WHERE Id=" + itemId)[0];
            string to = end.ToString("yyyy-MM-dd");
            int overlapping = int.Parse(db.Query("SELECT IFNULL(SUM(Quantity),0) FROM Rentals WHERE ItemId=" + itemId + " AND Returned=0 AND StartDate<=" + Db.Q(to))[0][0]);
            return int.Parse(row[0]) - int.Parse(row[1]) - int.Parse(row[2]) - overlapping;
        }

        private void ShowClients()
        {
            string text = ClientSearch == null ? "" : ClientSearch.Text.Trim().ToLowerInvariant();
            string phoneSearch = GetPhoneDigits(text);
            List<Client> rows = new List<Client>();
            foreach (Client client in allClients)
                if (client.Name.ToLowerInvariant().Contains(text) || client.Phone.ToLowerInvariant().Contains(text) || (phoneSearch.Length > 0 && GetPhoneDigits(client.Phone).Contains(phoneSearch))) rows.Add(client);
            ClientGrid.ItemsSource = rows;
        }

        private void ShowItems()
        {
            string text = ItemSearch == null ? "" : ItemSearch.Text.Trim().ToLowerInvariant();
            List<Item> rows = new List<Item>();
            foreach (Item item in allItems)
                if (item.Name.ToLowerInvariant().Contains(text) || item.Category.ToLowerInvariant().Contains(text)) rows.Add(item);
            ItemGrid.ItemsSource = rows;
        }

        private void ShowHistory()
        {
            string text = HistorySearch == null ? "" : HistorySearch.Text.Trim().ToLowerInvariant();
            List<Rental> rows = new List<Rental>();
            foreach (Rental rental in allHistory)
                if (rental.Client.ToLowerInvariant().Contains(text) || rental.Item.ToLowerInvariant().Contains(text)) rows.Add(rental);
            HistoryGrid.ItemsSource = rows;
        }

        private void ClientSearch_Changed(object sender, TextChangedEventArgs e) { if (loaded) ShowClients(); }
        private void ItemSearch_Changed(object sender, TextChangedEventArgs e) { if (loaded) ShowItems(); }
        private void HistorySearch_Changed(object sender, TextChangedEventArgs e) { if (loaded) ShowHistory(); }

        private void Phone_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (changingPhone) return;
            TextBox box = sender as TextBox;
            if (box == null || box.Text.Length == 0) return;
            changingPhone = true;
            string digits = GetPhoneDigits(box.Text);
            box.Text = FormatPhoneDigits(digits);
            box.CaretIndex = box.Text.Length;
            changingPhone = false;
        }

        private string GetPhoneDigits(string value)
        {
            StringBuilder result = new StringBuilder();
            foreach (char ch in value) if (char.IsDigit(ch)) result.Append(ch);
            string digits = result.ToString();
            if (digits.StartsWith("380")) digits = digits.Substring(3);
            if (digits.StartsWith("0")) digits = digits.Substring(1);
            return digits;
        }

        private string FormatPhoneDigits(string digits)
        {
            StringBuilder text = new StringBuilder("+380");
            if (digits.Length > 0)
            {
                text.Append(" (");
                text.Append(digits.Substring(0, Math.Min(2, digits.Length)));
                if (digits.Length >= 2) text.Append(") ");
                if (digits.Length > 2) text.Append(digits.Substring(2, Math.Min(3, digits.Length - 2)));
                if (digits.Length > 5) { text.Append("-"); text.Append(digits.Substring(5, Math.Min(2, digits.Length - 5))); }
                if (digits.Length > 7) { text.Append("-"); text.Append(digits.Substring(7)); }
            }
            else text.Append(" ");
            return text.ToString();
        }

        private bool TryGetPhone(string value, out string phone)
        {
            string digits = GetPhoneDigits(value);
            phone = "";
            if (digits.Length != 9) return false;
            phone = "+380" + digits;
            return true;
        }

        private string FormatSavedPhone(string value)
        {
            string phone;
            if (!TryGetPhone(value, out phone)) return value;
            return FormatPhoneDigits(phone.Substring(4));
        }

        private void AddClient_Click(object sender, RoutedEventArgs e)
        {
            string phone;
            if (ClientName.Text.Trim() == "" || !TryGetPhone(ClientPhone.Text, out phone)) { Show("Вкажіть ім’я та коректний український номер: 9 цифр після +380."); return; }
            db.Run("INSERT INTO Clients(Name,Phone) VALUES(" + Db.Q(ClientName.Text.Trim()) + "," + Db.Q(phone) + ")");
            ClientName.Clear(); ClientPhone.Clear(); LoadData(); Show("Клієнта додано.");
        }

        private void EditClient_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            int id = int.Parse(button.Tag.ToString());
            Client client = null;
            foreach (Client row in allClients) if (row.Id == id) client = row;
            if (client == null) return;

            Window win = new Window { Title = "Редагувати клієнта", Width = 390, Height = 270, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = System.Windows.Media.Brushes.White };
            StackPanel panel = new StackPanel { Margin = new Thickness(22) };
            panel.Children.Add(new TextBlock { Text = "Ім’я", Margin = new Thickness(0, 0, 0, 5), FontWeight = FontWeights.SemiBold });
            TextBox name = new TextBox { Text = client.Name, Margin = new Thickness(0, 0, 0, 14) };
            panel.Children.Add(name);
            panel.Children.Add(new TextBlock { Text = "Телефон (+380)", Margin = new Thickness(0, 0, 0, 5), FontWeight = FontWeights.SemiBold });
            TextBox phoneBox = new TextBox { Text = FormatSavedPhone(client.Phone), Margin = new Thickness(0, 0, 0, 18) };
            phoneBox.TextChanged += Phone_TextChanged;
            panel.Children.Add(phoneBox);
            Button save = new Button { Content = "Зберегти зміни", Padding = new Thickness(12, 8, 12, 8), HorizontalAlignment = HorizontalAlignment.Right, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 117, 82)), Foreground = System.Windows.Media.Brushes.White };
            save.Click += delegate { string cleanPhone; if (name.Text.Trim() == "" || !TryGetPhone(phoneBox.Text, out cleanPhone)) { MessageBox.Show(win, "Перевірте ім’я та український номер телефону.", "Перевірка", MessageBoxButton.OK, MessageBoxImage.Information); return; } db.Run("UPDATE Clients SET Name=" + Db.Q(name.Text.Trim()) + ",Phone=" + Db.Q(cleanPhone) + " WHERE Id=" + id); win.DialogResult = true; };
            panel.Children.Add(save);
            win.Content = panel;
            if (win.ShowDialog() == true) { LoadData(); Show("Дані клієнта оновлено."); }
        }

        private void CopyPhone_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string phone = button.Tag == null ? "" : button.Tag.ToString();
            try { Clipboard.SetText(phone); Show("Номер скопійовано: " + phone); }
            catch (InvalidOperationException) { Show("Не вдалося скопіювати номер. Спробуйте ще раз."); }
        }

        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            int qty;
            if (ItemName.Text.Trim() == "" || ItemCategory.Text.Trim() == "" || !int.TryParse(ItemQty.Text, out qty) || qty < 1) { Show("Вкажіть назву, категорію та кількість від 1."); return; }
            db.Run("INSERT INTO Items(Name,Category,Quantity,RepairCount,Status) VALUES(" + Db.Q(ItemName.Text.Trim()) + "," + Db.Q(ItemCategory.Text.Trim()) + "," + qty + ",0,'Доступний')");
            ItemName.Clear(); ItemCategory.Clear(); ItemQty.Text = "1"; LoadData(); Show("Інвентар додано.");
        }

        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            int id = int.Parse(button.Tag.ToString());
            Item item = null;
            foreach (Item row in allItems) if (row.Id == id) item = row;
            if (item == null) return;

            Window win = new Window { Title = "Редагувати інвентар", Width = 410, Height = 330, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = System.Windows.Media.Brushes.White };
            StackPanel panel = new StackPanel { Margin = new Thickness(22) };
            panel.Children.Add(new TextBlock { Text = "Назва", Margin = new Thickness(0, 0, 0, 5), FontWeight = FontWeights.SemiBold });
            TextBox name = new TextBox { Text = item.Name, Margin = new Thickness(0, 0, 0, 12) }; panel.Children.Add(name);
            panel.Children.Add(new TextBlock { Text = "Категорія", Margin = new Thickness(0, 0, 0, 5), FontWeight = FontWeights.SemiBold });
            TextBox category = new TextBox { Text = item.Category, Margin = new Thickness(0, 0, 0, 12) }; panel.Children.Add(category);
            panel.Children.Add(new TextBlock { Text = "Загальна кількість", Margin = new Thickness(0, 0, 0, 5), FontWeight = FontWeights.SemiBold });
            TextBox quantity = new TextBox { Text = item.Quantity.ToString(), Margin = new Thickness(0, 0, 0, 16) }; panel.Children.Add(quantity);
            Button save = new Button { Content = "Зберегти зміни", Padding = new Thickness(12, 8, 12, 8), HorizontalAlignment = HorizontalAlignment.Right, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 117, 82)), Foreground = System.Windows.Media.Brushes.White };
            save.Click += delegate { int qty; int accounted = GetReserved(item.Id) + item.RepairCount + item.WrittenOff; if (name.Text.Trim() == "" || category.Text.Trim() == "" || !int.TryParse(quantity.Text, out qty) || qty < accounted) { MessageBox.Show(win, "Введіть назву, категорію та кількість не меншу за видані, заплановані, ремонтні й списані одиниці (" + accounted + ").", "Перевірка", MessageBoxButton.OK, MessageBoxImage.Information); return; } db.Run("UPDATE Items SET Name=" + Db.Q(name.Text.Trim()) + ",Category=" + Db.Q(category.Text.Trim()) + ",Quantity=" + qty + " WHERE Id=" + id); win.DialogResult = true; };
            panel.Children.Add(save);
            win.Content = panel;
            if (win.ShowDialog() == true) { LoadData(); Show("Дані інвентарю оновлено."); }
        }

        private void Rent_Click(object sender, RoutedEventArgs e)
        {
            Client client = RentClient.SelectedItem as Client;
            Item item = RentItem.SelectedItem as Item;
            DateTime? start = RentStartDate.SelectedDate;
            DateTime? end = RentEndDate.SelectedDate;
            int qty;
            if (client == null || item == null || start == null || end == null || start.Value.Date < DateTime.Today || end.Value.Date < start.Value.Date || !int.TryParse(RentQty.Text, out qty) || qty < 1) { Show("Перевірте клієнта, спорядження, кількість і дати оренди."); return; }
            if (qty > GetAvailable(item.Id, end.Value.Date)) { Show("Такої кількості немає на весь обраний період."); return; }
            db.Run("INSERT INTO Rentals(ClientId,ItemId,Quantity,StartDate,EndDate,Returned) VALUES(" + client.Id + "," + item.Id + "," + qty + "," + Db.Q(start.Value.ToString("yyyy-MM-dd")) + "," + Db.Q(end.Value.ToString("yyyy-MM-dd")) + ",0)");
            RentQty.Text = "1"; LoadData(); Show("Оренду оформлено на обрані дати.");
        }

        private void Return_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            int id = int.Parse(button.Tag.ToString());
            db.Run("UPDATE Rentals SET Returned=1,ReturnedAt=" + Db.Q(DateTime.Today.ToString("yyyy-MM-dd")) + " WHERE Id=" + id + " AND Returned=0");
            LoadData(); Show("Повернення збережено в історії.");
        }

        private void Repair_Click(object sender, RoutedEventArgs e)
        {
            Item item = (sender as Button).DataContext as Item;
            if (item == null || item.Available <= 0 || GetReserved(item.Id) + item.RepairCount + item.WrittenOff >= item.Quantity) { Show("Немає вільної одиниці, щоб передати її в ремонт."); return; }
            db.Run("UPDATE Items SET RepairCount=RepairCount+1 WHERE Id=" + item.Id);
            LoadData(); Show("Одиницю інвентарю позначено як таку, що на ремонті.");
        }

        private void RepairDone_Click(object sender, RoutedEventArgs e)
        {
            Item item = (sender as Button).DataContext as Item;
            if (item == null || item.RepairCount <= 0) { Show("У цієї позиції немає одиниць на ремонті."); return; }
            db.Run("UPDATE Items SET RepairCount=RepairCount-1 WHERE Id=" + item.Id + " AND RepairCount>0");
            LoadData(); Show("Одиницю повернуто з ремонту.");
        }

        private void Writeoff_Click(object sender, RoutedEventArgs e)
        {
            Item item = (sender as Button).DataContext as Item;
            if (item == null || item.Available <= 0 || GetReserved(item.Id) + item.RepairCount + item.WrittenOff >= item.Quantity) { Show("Списати можна лише вільну, не заброньовану одиницю."); return; }
            if (MessageBox.Show("Списати одну одиницю «" + item.Name + "»?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            db.Run("UPDATE Items SET WrittenOff=WrittenOff+1 WHERE Id=" + item.Id + " AND Quantity-RepairCount-WrittenOff>0");
            LoadData(); Show("Одну одиницю списано.");
        }

        private void Show(string text) { Message.Text = text; }

        private class Client { public int Id { get; set; } public string Name { get; set; } public string Phone { get; set; } }
        private class Item { public int Id { get; set; } public string Name { get; set; } public string Category { get; set; } public int Quantity { get; set; } public int Rented { get; set; } public int RepairCount { get; set; } public int WrittenOff { get; set; } public int Available { get; set; } public string Status { get; set; } public string Label { get; set; } }
        private class Rental { public int Id { get; set; } public string Client { get; set; } public string Item { get; set; } public int Quantity { get; set; } public string StartDate { get; set; } public string EndDate { get; set; } public string ReturnedAt { get; set; } public string State { get; set; } }
    }

    public class Db
    {
        private IntPtr handle;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int RowCallback(IntPtr data, int count, IntPtr values, IntPtr names);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open(byte[] file, out IntPtr db);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_exec(IntPtr db, byte[] sql, RowCallback callback, IntPtr data, out IntPtr error);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);

        public Db(string path)
        {
            if (sqlite3_open(Utf8(path), out handle) != 0) throw new Exception("Не вдалося відкрити rent.db через системну SQLite.");
            Run("CREATE TABLE IF NOT EXISTS Clients(Id INTEGER PRIMARY KEY,Name TEXT,Phone TEXT)");
            Run("CREATE TABLE IF NOT EXISTS Items(Id INTEGER PRIMARY KEY,Name TEXT,Category TEXT,Status TEXT,Quantity INTEGER NOT NULL DEFAULT 1,RepairCount INTEGER NOT NULL DEFAULT 0,WrittenOff INTEGER NOT NULL DEFAULT 0)");
            Run("CREATE TABLE IF NOT EXISTS Rentals(Id INTEGER PRIMARY KEY,ClientId INTEGER,ItemId INTEGER,StartDate TEXT,EndDate TEXT,Returned INTEGER NOT NULL DEFAULT 0,Quantity INTEGER NOT NULL DEFAULT 1,ReturnedAt TEXT)");
            AddColumn("Items", "Quantity", "INTEGER NOT NULL DEFAULT 1");
            AddColumn("Items", "RepairCount", "INTEGER NOT NULL DEFAULT 0");
            AddColumn("Items", "WrittenOff", "INTEGER NOT NULL DEFAULT 0");
            AddColumn("Rentals", "Quantity", "INTEGER NOT NULL DEFAULT 1");
            AddColumn("Rentals", "ReturnedAt", "TEXT");
            Run("UPDATE Items SET Status='Доступний' WHERE Status='В оренді'");
            if (Query("SELECT Id FROM Items").Count == 0)
                Run("INSERT INTO Items(Name,Category,Status,Quantity,RepairCount) VALUES('Лижі','Зимовий спорт','Доступний',4,0),('Намет','Туризм','Доступний',3,0),('Велосипед','Велоспорт','Доступний',2,0)");
        }

        private void AddColumn(string table, string name, string type)
        {
            bool found = false;
            foreach (string[] row in Query("PRAGMA table_info(" + table + ")")) if (row[1] == name) found = true;
            if (!found) Run("ALTER TABLE " + table + " ADD COLUMN " + name + " " + type);
        }

        public void Run(string sql)
        {
            IntPtr error;
            int code = sqlite3_exec(handle, Utf8(sql), null, IntPtr.Zero, out error);
            if (code != 0)
            {
                string message = error == IntPtr.Zero ? "Помилка SQLite." : ReadUtf8(error);
                throw new Exception(message);
            }
        }

        public List<string[]> Query(string sql)
        {
            List<string[]> rows = new List<string[]>();
            RowCallback callback = delegate(IntPtr data, int count, IntPtr values, IntPtr names)
            {
                string[] row = new string[count];
                for (int i = 0; i < count; i++)
                {
                    IntPtr p = Marshal.ReadIntPtr(values, i * IntPtr.Size);
                    row[i] = p == IntPtr.Zero ? "" : ReadUtf8(p);
                }
                rows.Add(row);
                return 0;
            };
            IntPtr error;
            int code = sqlite3_exec(handle, Utf8(sql), callback, IntPtr.Zero, out error);
            GC.KeepAlive(callback);
            if (code != 0) throw new Exception(error == IntPtr.Zero ? "Помилка SQLite." : ReadUtf8(error));
            return rows;
        }

        public static string Q(string value) { return "'" + value.Replace("'", "''") + "'"; }
        private static byte[] Utf8(string value) { byte[] bytes = Encoding.UTF8.GetBytes(value); byte[] result = new byte[bytes.Length + 1]; Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length); return result; }
        private static string ReadUtf8(IntPtr p) { List<byte> bytes = new List<byte>(); byte b; while ((b = Marshal.ReadByte(p, bytes.Count)) != 0) bytes.Add(b); return Encoding.UTF8.GetString(bytes.ToArray()); }
        ~Db() { if (handle != IntPtr.Zero) sqlite3_close(handle); }
    }
}


