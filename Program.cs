using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Collections.Generic;

class Program
{
    public class Deprem
    {
        [JsonPropertyName("eventID")] public string EventID { get; set; }
        [JsonPropertyName("date")] public string Date { get; set; }
        [JsonPropertyName("latitude")] public string Latitude { get; set; }
        [JsonPropertyName("longitude")] public string Longitude { get; set; }
        [JsonPropertyName("depth")] public string Depth { get; set; }
        [JsonPropertyName("magnitude")] public string Magnitude { get; set; }
        [JsonPropertyName("location")] public string Location { get; set; }
    }

    static async Task Main(string[] args)
    {
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

        string connectionString = @"Server=.\SQLEXPRESS;Database=DepremProjesi;Trusted_Connection=True;TrustServerCertificate=True;";
        using SqlConnection connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Veritabanındaki en son kayıtlı tarihi buluyoruz (Eğer boşsa 18 Mayıs 2026'dan başlar)
        DateTime baslangicTarihi;
        string maxDateQuery = "SELECT ISNULL(MAX(Tarih), '2026-05-18') FROM Depremler";

        using (SqlCommand cmdMax = new SqlCommand(maxDateQuery, connection))
        {
            object result = await cmdMax.ExecuteScalarAsync();
            baslangicTarihi = Convert.ToDateTime(result);
        }

        baslangicTarihi = baslangicTarihi.AddDays(-1);
        DateTime bugun = DateTime.Now;

        string startStr = baslangicTarihi.ToString("yyyy-MM-dd");
        string endStr = bugun.ToString("yyyy-MM-dd");

        string url = $"https://deprem.afad.gov.tr/apiv2/event/filter?start={startStr}%2000:00:00&end={endStr}%2023:59:59&format=json";

        Console.WriteLine($"Akıllı Senkronizasyon: {startStr} ile {endStr} arasındaki tüm depremler taranıyor...\n");

        try
        {
            string jsonData = await client.GetStringAsync(url);
            var depremler = JsonSerializer.Deserialize<List<Deprem>>(jsonData);

            int yeniKayitSayisi = 0;
            foreach (var d in depremler)
            {
                try
                {
                    // Hiçbir koordinat sınırlaması yapmadan gelen tüm verileri doğrudan kaydediyoruz
                    string query = @"
                        INSERT INTO Depremler (EventId, Tarih, Enlem, Boylam, Derinlik, Siddet, Yer)
                        VALUES (@EventId, @Tarih, @Enlem, @Boylam, @Derinlik, @Siddet, @Yer)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EventId", d.EventID);
                        command.Parameters.AddWithValue("@Tarih", DateTime.Parse(d.Date));
                        command.Parameters.AddWithValue("@Enlem", double.Parse(d.Latitude, CultureInfo.InvariantCulture));
                        command.Parameters.AddWithValue("@Boylam", double.Parse(d.Longitude, CultureInfo.InvariantCulture));
                        command.Parameters.AddWithValue("@Derinlik", double.Parse(d.Depth, CultureInfo.InvariantCulture));
                        command.Parameters.AddWithValue("@Siddet", double.Parse(d.Magnitude, CultureInfo.InvariantCulture));
                        command.Parameters.AddWithValue("@Yer", d.Location ?? (object)DBNull.Value);

                        await command.ExecuteNonQueryAsync();
                        yeniKayitSayisi++;
                    }
                }
                catch (SqlException)
                {
                    // Zaten var olan depremler atlanır
                }
            }
            Console.WriteLine($"Senkronizasyon Başarılı! Eklenen yeni deprem sayısı: {yeniKayitSayisi}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hata oluştu: {ex.Message}");
        }
    }
}
