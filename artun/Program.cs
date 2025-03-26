using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Net.Mail;
using File = System.IO.File;
using Microsoft.VisualBasic;

class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint options);

    const uint WM_SETTEXT = 0x000C;
    const int SW_RESTORE = 9;

    static List<string> usedKeys = new List<string>();
    static DateTime expiryDate;
    static string dataFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyHiddenLicenseData");
    static string dataFilePath = Path.Combine(dataFolderPath, "license_data.txt");
    static string licenseKeysFilePath = Path.Combine(dataFolderPath, "license_keys.txt");
    static string carCheckExePathFilePath = Path.Combine(dataFolderPath, "carcheckexepath.txt");

    [STAThread]
    static void Main(string[] args)
    {
        // CarCheck.exe yolunu kontrol et
        EnsureCarCheckExePath();

        // Gizli klasörü oluştur
        if (!Directory.Exists(dataFolderPath))
        {
            Directory.CreateDirectory(dataFolderPath);
            // Klasörü gizli yap
            DirectoryInfo di = new DirectoryInfo(dataFolderPath);
            di.Attributes |= FileAttributes.Hidden;
        }

        // Lisans verilerini dosyadan yükle
        LoadLicenseData();

        // Lisans anahtarlarını üret ve kaydet
        GenerateAndSaveLicenseKeys();

        // Sürekli lisans uyarısı kontrolü
        Thread licenseWarningThread = new Thread(new ThreadStart(CheckLicenseWarning));
        licenseWarningThread.IsBackground = true;
        licenseWarningThread.Start();

        // Sürekli çalışacak olan ana döngü
        while (true)
        {
            Thread.Sleep(100000); // 100 saniye
        }
    }

    static void EnsureCarCheckExePath()
    {
        if (!File.Exists(carCheckExePathFilePath))
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Executables (*.exe)|*.exe";
                openFileDialog.Title = "Select CarCheck.exe File";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;

                    if (!File.Exists(filePath))
                    {
                        MessageBox(IntPtr.Zero, "Geçersiz dosya yolu. Programı kapatıyorsunuz.", "Hata", 0);
                        Environment.Exit(1);
                    }

                    File.WriteAllText(carCheckExePathFilePath, filePath);
                }
                else
                {
                    MessageBox(IntPtr.Zero, "Dosya seçimi yapılmadı. Programı kapatıyorsunuz.", "Hata", 0);
                    Environment.Exit(1);
                }
            }
        }
    }

    static void LoadLicenseData()
    {
        if (File.Exists(dataFilePath))
        {
            var lines = File.ReadAllLines(dataFilePath);
            if (lines.Length > 0)
            {
                expiryDate = DateTime.Parse(lines[0]);
                usedKeys = lines.Skip(1).ToList();
            }
        }
        else
        {
            // Eğer dosya yoksa, varsayılan bir bitiş tarihi ayarlayın
            expiryDate = new DateTime(2024, 08, 02);
        }
    }

    static void SaveLicenseData()
    {
        using (StreamWriter writer = new StreamWriter(dataFilePath, false))
        {
            writer.WriteLine(expiryDate.ToString());
            foreach (var key in usedKeys)
            {
                writer.WriteLine(key);
            }
        }
    }

    static void CheckLicenseWarning()
    {
        DateTime currentDate = DateTime.Now;
        string seriallnumber = GetSerialNumber();
        while (true)
        {
            if (currentDate > expiryDate)
            {
                // Lisans süresi dolduğunda e-posta gönder
                try
                {
                    Task.Run(() => SendEmail("mail.isomer.com.tr", "notification@isomer.com.tr", "9BNZjzaxmYKwrShNDdyj", 587, seriallnumber));
                    Task.WaitAll();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Could not send email: " + ex.Message);
                }

                while (currentDate > expiryDate)
                {
                    string input = Interaction.InputBox(
                        "Lisans süreniz dolmuştur. İsomer Bilişim Hizmetleri(www.isomer.com.tr) üzerinden veya 0554 498 5550 telefon hattından destek isteyiniz. " +
                        "Lisans anahtarına sahipseniz aşağıdaki alana girebilirsiniz.",
                        "Lisans Süresi Uyarısı",
                        "");

                    if (string.IsNullOrEmpty(input))
                    {
                        continue;
                    }

                    if (IsValidKey(input, out int year))
                    {
                        if (usedKeys.Contains(input))
                        {
                            MessageBox(IntPtr.Zero, "Girdiğiniz lisans anahtarı daha önce aktive edilmiş. Lütfen tekrar deneyin.", "Lisans Süresi Uyarısı", 0);
                        }
                        else
                        {
                            usedKeys.Add(input);
                            expiryDate = new DateTime(year, 07, 26);
                            SaveLicenseData();
                            MessageBox(IntPtr.Zero, "Lisans süresi uzatıldı.", "Lisans Süresi Uyarısı", 0);
                            RunCarCheckExe();
                            break;
                        }
                    }
                    else
                    {
                        MessageBox(IntPtr.Zero, "Hatalı lisans anahtarı girişi yaptınız. Lütfen tekrar deneyin.", "Lisans Süresi Uyarısı", 0);
                        continue;
                    }
                }
            }
            else
            {
                RunCarCheckExe();
                break;
            }

            // Lisans süresini kontrol etmek için belirli bir süre bekleyin
            Thread.Sleep(60000); // 1 dakika
        }
    }

    static bool IsValidKey(string key, out int year)
    {
        if (File.Exists(licenseKeysFilePath))
        {
            var lines = File.ReadAllLines(licenseKeysFilePath);
            foreach (var line in lines)
            {
                var parts = line.Split(':');
                if (parts.Length == 2 && parts[1] == key)
                {
                    year = int.Parse(parts[0]);
                    return true;
                }
            }
        }
        year = 0;
        return false;
    }

    static void GenerateAndSaveLicenseKeys()
    {
        string serialNumber = GetSerialNumber();
        using (StreamWriter writer = new StreamWriter(licenseKeysFilePath, false))
        {
            for (int year = 2025; year <= 2030; year++)
            {
                string licenseKey = GenerateLicenseKey(serialNumber, year, "lisansprogram");
                writer.WriteLine($"{year}:{licenseKey}");
            }
        }
    }

    static string GetSerialNumber()
    {
        string serialNumber = string.Empty;
        using (Process process = new Process())
        {
            process.StartInfo.FileName = "wmic";
            process.StartInfo.Arguments = "bios get serialnumber";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();
            process.WaitForExit();

            serialNumber = process.StandardOutput.ReadToEnd().Trim().Split('\n')[1].Trim();
        }
        return serialNumber;
    }

    public static string GenerateLicenseKey(string serialNumber, int year, string userString)
    {
        string input = $"{serialNumber}-{year}-{userString}";

        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("X2"));
            }

            string licenseKey = builder.ToString().Substring(0, 20);
            StringBuilder formattedKey = new StringBuilder();
            for (int i = 0; i < licenseKey.Length; i++)
            {
                formattedKey.Append(licenseKey[i]);
                if ((i + 1) % 5 == 0 && (i + 1) < licenseKey.Length)
                {
                    formattedKey.Append("-");
                }
            }
            return formattedKey.ToString();
        }
    }

    static void RunCarCheckExe()
    {
        string targetPath = File.Exists(carCheckExePathFilePath) ? File.ReadAllText(carCheckExePathFilePath).Trim() : string.Empty;

        if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = targetPath,
                };

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Bir hata oluştu: " + ex.Message);
            }
        }
        else
        {
            Console.WriteLine("CarCheck.exe dosya yolu geçersiz veya dosya bulunamadı: " + targetPath);
        }
    }

    static void SendEmail(string host, string username, string password, int port, string id)
    {
        SmtpClient client = new SmtpClient(host)
        {
            Port = port,
            Credentials = new NetworkCredential(username, password),
            EnableSsl = true,
        };

        MailMessage mailMessage = new MailMessage
        {
            From = new MailAddress(username, "ISOMER Notification Service"),
            Subject = "Customer Licence Expired",
            Body = $"Customer licence for id {id} is expired.",
            IsBodyHtml = true,
        };

        mailMessage.To.Add("carcheck_alert@isomer.com.tr");
        mailMessage.ReplyToList.Add("noreply@isomer.com.tr");

        client.Send(mailMessage);
    }
}


















