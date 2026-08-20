using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Text.Json;




[Route("api/[controller]")] // Bu, adresin /api/fay olmasını sağlar
[ApiController]
public class FayController : ControllerBase
{

    private readonly string _connectionString = "Server=.\\SQLEXPRESS;Database=DepremProjesi;Trusted_Connection=True;TrustServerCertificate=True;";

    [HttpGet("faylari-getir")] // Bu da /api/fay/faylari-getir yapar
    public IActionResult FaylariGetir()
    {
        var fayListesi = new List<object>();

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            string query = "SELECT FayAdi, Aktivite, KaymaTuru, Uzunluk, KoordinatJson FROM DiriFaylar WHERE KoordinatJson IS NOT NULL";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string koordinatStr = reader["KoordinatJson"].ToString();
                        var koordinatlar = JsonSerializer.Deserialize<object>(koordinatStr);

                        // Leaflet'in çok sevdiği GeoJSON Feature yapısını oluşturuyoruz
                        var feature = new
                        {
                            type = "Feature",
                            properties = new
                            {
                                fayAdi = reader["FayAdi"]?.ToString() ?? "Bilinmeyen Fay",
                                aktivite = reader["Aktivite"]?.ToString() ?? "",
                                kaymaTuru = reader["KaymaTuru"]?.ToString() ?? "",
                                uzunluk = reader["Uzunluk"] != DBNull.Value ? Convert.ToDouble(reader["Uzunluk"]) : 0
                            },
                            geometry = new
                            {
                                type = "MultiLineString",
                                coordinates = new[] { koordinatlar } // Çizgi koordinatları
                            }
                        };

                        fayListesi.Add(feature);
                    }
                }
            }
        }

        var geoJsonCollection = new
        {
            type = "FeatureCollection",
            features = fayListesi
        };

        return Ok(geoJsonCollection);
    }
}










