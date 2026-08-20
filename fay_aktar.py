import xml.etree.ElementTree as ET
import json
import pyodbc

def veri_aktar():
    dosya_adi = "response.adding"  # GML/XML dosyanın adı
    
    print("XML dosyası okunuyor ve ayrıştırılıyor...")
    tree = ET.parse(dosya_adi)
    root = tree.getroot()

    fay_listesi = []

    # Tüm member elemanlarını buluyoruz (namespace bağımsız)
    for member in root.findall('.//{*}member'):
        # İçindeki ana düğümü buluyoruz (TDFH ile başlayan)
        tdfh = None
        for child in member:
            if "TDFH" in child.tag:
                tdfh = child
                break
        
        if tdfh is None:
            # Eğer doğrudan bulamazsa ilk çocuğu al
            children = list(member)
            if len(children) > 0:
                tdfh = children[0]
            else:
                continue

        # Değerleri esnek bir şekilde arayalım (tag adı ne olursa olsun içerdiği kelimeye bakarak)
        object_id = "0"
        fay_adi = "Bilinmeyen Fay"
        segment_adi = ""
        aktivite = ""
        kayma_turu = ""
        uzunluk = "0"

        for elem in tdfh:
            tag_name = elem.tag.lower()
            val = elem.text if elem.text else ""
            
            if "objectid" in tag_name:
                object_id = val
            elif "fayadi" in tag_name:
                fay_adi = val
            elif "segmentadi" in tag_name:
                segment_adi = val if val != "<Null>" else ""
            elif "aktivite" in tag_name:
                aktivite = val
            elif "kayma_turu" in tag_name:
                kayma_turu = val
            elif "uzunluk" in tag_name:
                uzunluk = val

        # Koordinatları (posList) bulma
        pos_list_elem = None
        for elem in tdfh.iter():
            if "posList" in elem.tag:
                pos_list_elem = elem
                break
        
        koordinat_ciftleri = []
        wkt_koordinatlari = []

        if pos_list_elem is not None and pos_list_elem.text:
            ham_degerler = pos_list_elem.text.strip().split()
            
            # 4'erli artıyoruz (Boylam, Enlem, 0, -Infinity)
            for i in range(0, len(ham_degerler), 4):
                try:
                    boylam = float(ham_degerler[i])
                    enlem = float(ham_degerler[i+1])
                    
                    koordinat_ciftleri.append([enlem, boylam])
                    wkt_koordinatlari.append(f"{boylam} {enlem}")
                except (ValueError, IndexError):
                    continue

        wkt_linestring = f"LINESTRING({', '.join(wkt_koordinatlari)})" if wkt_koordinatlari else None

        if wkt_linestring:
            fay_listesi.append({
                'object_id': float(object_id) if object_id else 0,
                'fay_adi': fay_adi,
                'segment_adi': segment_adi,
                'aktivite': aktivite,
                'kayma_turu': kayma_turu,
                'uzunluk': float(uzunluk) if uzunluk and uzunluk != '<Null>' else 0,
                'koordinatlar_json': json.dumps(koordinat_ciftleri),
                'wkt_geom': wkt_linestring
            })

    print(f"Toplam {len(fay_listesi)} adet geçerli fay bulundu. SQL'e aktarılıyor...")

    # SQL Server Bağlantısı
    conn = pyodbc.connect(
        'DRIVER={ODBC Driver 17 for SQL Server};'
        'SERVER=.\\SQLEXPRESS;'
        'DATABASE=DepremProjesi;'
        'Trusted_Connection=yes;'
    )
    cursor = conn.cursor()

    for row in fay_listesi:
        query = """
        INSERT INTO DiriFaylar (ObjectId, FayAdi, SegmentAdi, Aktivite, KaymaTuru, Uzunluk, Geometri, KoordinatJson)
        VALUES (?, ?, ?, ?, ?, ?, geometry::STGeomFromText(?, 4326), ?)
        """
        cursor.execute(query, (
            row['object_id'],
            row['fay_adi'],
            row['segment_adi'],
            row['aktivite'],
            row['kayma_turu'],
            row['uzunluk'],
            row['wkt_geom'],
            row['koordinatlar_json']
        ))

    conn.commit()
    cursor.close()
    conn.close()
    print("İşlem Başarılı! Tüm fay hatları doğru isimlerle veritabanına kaydedildi.")

if __name__ == '__main__':
    veri_aktar()