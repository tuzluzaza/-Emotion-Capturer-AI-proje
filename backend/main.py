import os
import shutil
import glob
from fastapi import FastAPI, UploadFile, File, Form, Depends, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from sqlalchemy.orm import Session
from deepface import DeepFace
import pandas as pd

import models
from database import engine, get_db

# Veritabanı tablolarını oluştur
models.Base.metadata.create_all(bind=engine)

app = FastAPI(title="Face Recognition and Emotion Analysis API")

# CORS ayarları
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Referans yüzlerin bulunduğu klasör
FACE_DB_PATH = "face_db"
os.makedirs(FACE_DB_PATH, exist_ok=True)

# Veri dosyaları
DATA_PATH = "data"
os.makedirs(DATA_PATH, exist_ok=True)
ROLES_FILE = os.path.join(DATA_PATH, "roles.txt")
ROOMS_FILE = os.path.join(DATA_PATH, "rooms.txt")
PERSONS_FILE = os.path.join(DATA_PATH, "persons.txt")
LOG_FILE = os.path.join(DATA_PATH, "access_log.txt")


# --- Yardımcı Fonksiyonlar ---

def get_roles() -> dict:
    """roles.txt'i okur. {rol_adı: [izinli_odalar]} döndürür."""
    roles = {}
    if os.path.exists(ROLES_FILE):
        with open(ROLES_FILE, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line and "|" in line:
                    parts = line.split("|")
                    role_name = parts[0].strip()
                    rooms = [r.strip() for r in parts[1].split(",")] if len(parts) > 1 else []
                    roles[role_name] = rooms
    return roles


def get_rooms() -> dict:
    """rooms.txt'i okur. {oda_adı: gerekli_duygu} döndürür."""
    rooms = {}
    if os.path.exists(ROOMS_FILE):
        with open(ROOMS_FILE, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line and "|" in line:
                    parts = line.split("|")
                    room_name = parts[0].strip()
                    required_emotion = parts[1].strip() if len(parts) > 1 else "happy"
                    rooms[room_name] = required_emotion
    return rooms


def get_persons() -> dict:
    """persons.txt'i okur. {kişi_adı: rol} döndürür."""
    persons = {}
    if os.path.exists(PERSONS_FILE):
        with open(PERSONS_FILE, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line and "|" in line:
                    parts = line.split("|")
                    person_name = parts[0].strip()
                    role = parts[1].strip() if len(parts) > 1 else ""
                    persons[person_name] = role
    return persons


def save_person_role(person_name: str, role: str):
    """Kişiyi persons.txt'e ekler veya günceller."""
    persons = get_persons()
    persons[person_name] = role
    with open(PERSONS_FILE, "w", encoding="utf-8") as f:
        for name, r in persons.items():
            f.write(f"{name}|{r}\n")


def write_log(event: str, person: str = "-", room: str = "-", result: str = "-", detail: str = ""):
    """access_log.txt'e zaman damgalı kayıt ekler."""
    from datetime import datetime
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    line = f"{timestamp} | Kişi: {person} | Etkinlik: {event} | Oda: {room} | Sonuç: {result}"
    if detail:
        line += f" | Detay: {detail}"
    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(line + "\n")


def detect_face(image_path: str) -> bool:
    """Fotoğrafta insan yüzü var mı kontrol eder. Varsa True döner."""
    try:
        faces = DeepFace.extract_faces(img_path=image_path, enforce_detection=True)
        return faces is not None and len(faces) > 0
    except Exception:
        return False


def clear_deepface_cache():
    """DeepFace'in PKL önbellek dosyalarını temizler."""
    for pattern in [
        os.path.join(FACE_DB_PATH, "representations_*.pkl"),
        os.path.join(FACE_DB_PATH, "ds_model_*.pkl"),
    ]:
        for cache_file in glob.glob(pattern):
            try:
                os.remove(cache_file)
            except Exception:
                pass


# ==========================================
# MEVCUT ENDPOİNT'LER
# ==========================================

@app.post("/recognize")
async def recognize_face(file: UploadFile = File(...)):
    """Gelen fotoğrafı alır, veritabanında tanır ve kişinin rolünü döndürür."""
    temp_file_path = f"temp_recog_{file.filename}"
    with open(temp_file_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)

    try:
        # 1. Veritabanı boşsa direkt unknown dön (model yüklemeye gerek yok)
        db_images = [f for f in os.listdir(FACE_DB_PATH) if f.endswith(('.jpg', '.jpeg', '.png'))]
        if not db_images:
            return {"status": "unknown", "message": "Kayıtlı kişi bulunmuyor."}

        # 2. Yüz tespiti
        if not detect_face(temp_file_path):
            return {"status": "no_face", "message": "Fotoğrafta insan yüzü tespit edilemedi."}

        # 3. Yüz eşleştirme
        dfs = DeepFace.find(
            img_path=temp_file_path,
            db_path=FACE_DB_PATH,
            model_name="Facenet",
            enforce_detection=False
        )

        if len(dfs) > 0 and not dfs[0].empty:
            matched_image_path = dfs[0].iloc[0]['identity']
            recognized_person = os.path.basename(matched_image_path).split('.')[0]

            # Kişinin rolünü de ekle
            persons = get_persons()
            role = persons.get(recognized_person, "")

            return {"status": "found", "person": recognized_person, "role": role}

        return {"status": "unknown"}

    except ValueError as ve:
        return {"status": "no_face", "message": str(ve)}
    except Exception as e:
        print(f"[recognize] Hata: {e}")
        return {"status": "unknown"}
    finally:
        if os.path.exists(temp_file_path):
            os.remove(temp_file_path)


@app.post("/register")
async def register_person(
    person_name: str = Form(...),
    file: UploadFile = File(...),
    role: str = Form(default="")
):
    """Yeni bir kişiyi sisteme kaydeder (fotoğraf + opsiyonel rol)."""
    try:
        file_extension = file.filename.split('.')[-1] if '.' in file.filename else "jpg"
        save_path = os.path.join(FACE_DB_PATH, f"{person_name}.{file_extension}")

        with open(save_path, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)

        # Önbelleği temizle
        clear_deepface_cache()

        # Rol atandıysa persons.txt'e kaydet
        if role.strip():
            save_person_role(person_name, role.strip())

        return {"status": "success", "message": f"{person_name} başarıyla eklendi."}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/analyze_emotion")
async def analyze_emotion(file: UploadFile = File(...), db: Session = Depends(get_db)):
    """Bağımsız duygu analizi yapar."""
    temp_file_path = f"temp_emotion_{file.filename}"
    with open(temp_file_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)

    try:
        analysis = DeepFace.analyze(
            img_path=temp_file_path,
            actions=['emotion'],
            enforce_detection=False
        )

        if isinstance(analysis, list):
            analysis = analysis[0]

        raw_emotions = analysis.get('emotion', {})
        emotion_results = {k: float(v) for k, v in raw_emotions.items()}
        dominant_emotion = analysis.get('dominant_emotion', 'Unknown')

        db_record = models.AnalysisHistory(
            person_name="Anlık Duygu Analizi",
            dominant_emotion=dominant_emotion
        )
        db.add(db_record)
        db.commit()

        return {
            "status": "success",
            "dominant_emotion": dominant_emotion,
            "emotions": emotion_results
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        if os.path.exists(temp_file_path):
            os.remove(temp_file_path)


# ==========================================
# YENİ ENDPOİNT'LER - ROL/ODA/KİŞİ YÖNETİMİ
# ==========================================

@app.get("/roles")
async def list_roles():
    """Tüm rolleri ve her rolün erişebileceği odaları listeler."""
    roles = get_roles()
    result = [{"name": name, "allowed_rooms": rooms} for name, rooms in roles.items()]
    return {"roles": result}


@app.get("/rooms")
async def list_rooms():
    """Tüm odaları ve gerekli duyguları listeler."""
    rooms = get_rooms()
    result = [{"name": name, "required_emotion": emotion} for name, emotion in rooms.items()]
    return {"rooms": result}


@app.get("/persons")
async def list_persons():
    """persons.txt'deki tüm kişileri ve rollerini listeler."""
    persons = get_persons()
    result = [{"name": name, "role": role} for name, role in persons.items()]
    return {"persons": result}


@app.post("/persons/assign")
async def assign_person_role(person_name: str = Form(...), role: str = Form(...)):
    """Var olan bir kişiye rol atar veya günceller."""
    roles = get_roles()
    if role not in roles:
        raise HTTPException(status_code=400, detail=f"'{role}' rolü tanımlı değil.")
    save_person_role(person_name, role)
    return {"status": "success", "message": f"{person_name} kişisine '{role}' rolü atandı."}


@app.post("/persons/update_role")
async def update_person_role(person_name: str = Form(...), new_role: str = Form(...)):
    """Kayıtlı bir kişinin rolünü günceller."""
    persons = get_persons()
    if person_name not in persons:
        raise HTTPException(status_code=404, detail=f"'{person_name}' adlı kişi bulunamadı.")
    roles = get_roles()
    if new_role not in roles:
        raise HTTPException(status_code=400, detail=f"'{new_role}' rolü tanımlı değil.")
    old_role = persons[person_name]
    save_person_role(person_name, new_role)
    write_log("Rol Güncellendi", person=person_name, result="Başarılı",
              detail=f"{old_role} → {new_role}")
    return {"status": "success", "message": f"{person_name} kişisinin rolü '{new_role}' olarak güncellendi."}


@app.get("/logs")
async def get_logs(limit: int = 100):
    """Log dosyasını okur. En son 'limit' satırı döndürür."""
    if not os.path.exists(LOG_FILE):
        return {"logs": []}
    with open(LOG_FILE, "r", encoding="utf-8") as f:
        lines = f.readlines()
    # Son limit satırı al, en yeniden en eskiye sırala
    recent = list(reversed([l.strip() for l in lines if l.strip()]))[:limit]
    return {"logs": recent}


@app.post("/logs/analyze")
async def analyze_logs():
    """Log kayıtlarını Gemini AI ile analiz eder. API anahtarı sunucu tarafında config.py'de saklanır."""
    try:
        from config import GEMINI_API_KEY
        if not GEMINI_API_KEY or GEMINI_API_KEY == "BURAYA_ANAHTARINIZI_YAZIN":
            raise HTTPException(status_code=503, detail="Gemini API anahtarı henüz yapılandırılmamış. Lütfen backend/config.py dosyasını düzenleyin.")
    except ImportError:
        raise HTTPException(status_code=503, detail="config.py bulunamadı.")

    if not os.path.exists(LOG_FILE):
        return {"analysis": "Henüz analiz edilecek log kaydı yok.", "log_count": 0}

    with open(LOG_FILE, "r", encoding="utf-8") as f:
        lines = [l.strip() for l in f.readlines() if l.strip()]

    if not lines:
        return {"analysis": "Log dosyası boş, analiz yapılamıyor.", "log_count": 0}

    # Son 100 kaydı al
    recent_logs = "\n".join(lines[-100:])

    prompt = f"""Aşağıdaki bina erişim kontrol sistemi loglarını analiz et ve Türkçe, kapsamlı bir rapor hazırla.

LOG KAYITLARI:
{recent_logs}

Lütfen şu başlıklar altında analiz yap:

1. 🛡️ **Güvenlik Durumu**: Sistemde şüpheli aktivite var mı? Başarısız giriş denemeleri ne kadar?
2. 👥 **Kullanıcı Aktivitesi**: Kaç farklı kişi sistemi kullanmış? Yoğun kullanım saatleri var mı?
3. 😊 **Duygu Analizi**: Duygu tespiti ne kadar başarılı? Hangi duygu ret kararlarına yol açıyor?
4. 🚪 **Oda Erişim Örüntüleri**: Hangi odalar en çok kullanılıyor? Yetkisiz giriş denemeleri var mı?
5. 🔑 **Rol Yönetimi**: Rol değişiklikleri yapılmış mı? Bunlar normal mi?
6. ⚠️ **Öneriler**: Sistemi daha güvenli hale getirmek için öneriler.

Analizi net, anlaşılır ve pratik tut. Emoji kullanarak okunabilirliği artır."""

    try:
        import google.generativeai as genai
        genai.configure(api_key=GEMINI_API_KEY)
        model = genai.GenerativeModel("gemini-flash-latest")
        response = model.generate_content(prompt)
        return {"analysis": response.text, "log_count": len(lines)}
    except Exception as e:
        error_msg = str(e)
        if "API_KEY" in error_msg.upper() or "INVALID" in error_msg.upper():
            raise HTTPException(status_code=401, detail="Geçersiz Gemini API anahtarı. config.py'yi kontrol edin.")
        raise HTTPException(status_code=500, detail=f"Gemini API hatası: {error_msg}")


@app.post("/logs/clear")
async def clear_logs():
    """Log dosyasını temizler."""
    with open(LOG_FILE, "w", encoding="utf-8") as f:
        f.write("")
    return {"status": "success", "message": "Loglar temizlendi."}


@app.post("/rooms/add")
async def add_room(room_name: str = Form(...), required_emotion: str = Form(...)):
    """Yeni oda ekler (rooms.txt'e yazar)."""
    rooms = get_rooms()
    if room_name in rooms:
        raise HTTPException(status_code=400, detail=f"'{room_name}' odası zaten mevcut.")
    with open(ROOMS_FILE, "a", encoding="utf-8") as f:
        f.write(f"{room_name}|{required_emotion}\n")
    return {"status": "success", "message": f"'{room_name}' odası eklendi."}


@app.post("/rooms/delete")
async def delete_room(room_name: str = Form(...)):
    """Odayı siler (rooms.txt'den kaldırır)."""
    rooms = get_rooms()
    if room_name not in rooms:
        raise HTTPException(status_code=404, detail=f"'{room_name}' odası bulunamadı.")
    rooms.pop(room_name)
    with open(ROOMS_FILE, "w", encoding="utf-8") as f:
        for name, emotion in rooms.items():
            f.write(f"{name}|{emotion}\n")
    return {"status": "success", "message": f"'{room_name}' odası silindi."}


@app.post("/rooms/update_emotion")
async def update_room_emotion(room_name: str = Form(...), required_emotion: str = Form(...)):
    """Odanın gerekli duygusunu günceller."""
    rooms = get_rooms()
    if room_name not in rooms:
        raise HTTPException(status_code=404, detail=f"'{room_name}' odası bulunamadı.")
    rooms[room_name] = required_emotion
    with open(ROOMS_FILE, "w", encoding="utf-8") as f:
        for name, emotion in rooms.items():
            f.write(f"{name}|{emotion}\n")
    return {"status": "success", "message": f"'{room_name}' odasının duygusu güncellendi."}


@app.post("/roles/add")
async def add_role(role_name: str = Form(...), allowed_rooms: str = Form(default="")):
    """Yeni rol ekler (roles.txt'e yazar). allowed_rooms virgülle ayrılmış liste."""
    roles = get_roles()
    if role_name in roles:
        raise HTTPException(status_code=400, detail=f"'{role_name}' rolü zaten mevcut.")
    with open(ROLES_FILE, "a", encoding="utf-8") as f:
        f.write(f"{role_name}|{allowed_rooms}\n")
    return {"status": "success", "message": f"'{role_name}' rolü eklendi."}


@app.post("/roles/delete")
async def delete_role(role_name: str = Form(...)):
    """Rolü siler (roles.txt'den kaldırır)."""
    roles = get_roles()
    if role_name not in roles:
        raise HTTPException(status_code=404, detail=f"'{role_name}' rolü bulunamadı.")
    if role_name == "Müdür":
        raise HTTPException(status_code=400, detail="Müdür rolü silinemez.")
    roles.pop(role_name)
    with open(ROLES_FILE, "w", encoding="utf-8") as f:
        for name, rooms in roles.items():
            f.write(f"{name}|{','.join(rooms)}\n")
    return {"status": "success", "message": f"'{role_name}' rolü silindi."}


@app.post("/roles/update_rooms")
async def update_role_rooms(role_name: str = Form(...), allowed_rooms: str = Form(...)):
    """Bir rolün izinli odalarını günceller."""
    roles = get_roles()
    if role_name not in roles:
        raise HTTPException(status_code=404, detail=f"'{role_name}' rolü bulunamadı.")
    roles[role_name] = [r.strip() for r in allowed_rooms.split(",") if r.strip()]
    with open(ROLES_FILE, "w", encoding="utf-8") as f:
        for name, rooms in roles.items():
            f.write(f"{name}|{','.join(rooms)}\n")
    return {"status": "success", "message": f"'{role_name}' rolünün odaları güncellendi."}


# ==========================================
# ODA ERİŞİM KONTROLÜ
# ==========================================

@app.post("/access/check")
async def check_room_access(
    file: UploadFile = File(...),
    room_name: str = Form(...),
    db: Session = Depends(get_db)
):
    """
    Fotoğraf + oda adı alır.
    1. Yüz tespiti
    2. Yüz tanıma
    3. Rol yetkisi kontrolü
    4. Duygu analizi
    5. Gerekli duygu karşılaştırması
    Sonuç: access_granted | access_denied_role | access_denied_emotion | unknown_person | no_role | no_face
    """
    temp_file_path = f"temp_access_{file.filename}"
    with open(temp_file_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)

    try:
        # 1. Veritabanı boşsa önce kontrol et
        db_images = [f for f in os.listdir(FACE_DB_PATH) if f.endswith(('.jpg', '.jpeg', '.png'))]
        if not db_images:
            write_log("Giriş Denemesi", room=room_name, result="Bilinmeyen", detail="Yüz veritabanı boş")
            return {"status": "unknown_person", "message": "Yüz veritabanı boş."}

        # 2. Yüz tespiti
        if not detect_face(temp_file_path):
            write_log("Giriş Denemesi", room=room_name, result="Reddedildi", detail="Yüz tespit edilemedi")
            return {"status": "no_face", "message": "Fotoğrafta insan yüzü tespit edilemedi."}

        # 3. Yüz tanıma
        dfs = DeepFace.find(
            img_path=temp_file_path,
            db_path=FACE_DB_PATH,
            model_name="Facenet",
            enforce_detection=False
        )

        if not (len(dfs) > 0 and not dfs[0].empty):
            write_log("Giriş Denemesi", room=room_name, result="Bilinmeyen Kişi", detail="Yüz eşleşmedi")
            return {"status": "unknown_person", "message": "Kişi tanınamadı."}

        matched_image_path = dfs[0].iloc[0]['identity']
        person_name = os.path.basename(matched_image_path).split('.')[0]

        # 3. Rol kontrolü
        persons = get_persons()
        person_role = persons.get(person_name, "")

        if not person_role:
            write_log("Giriş Denemesi", person=person_name, room=room_name, result="Reddedildi", detail="Rol atanmamış")
            return {
                "status": "no_role",
                "person": person_name,
                "message": f"{person_name} kişisine henüz bir rol atanmamış."
            }

        roles = get_roles()
        allowed_rooms = roles.get(person_role, [])

        if room_name not in allowed_rooms:
            write_log("Giriş Denemesi", person=person_name, room=room_name, result="Reddedildi",
                      detail=f"Rol yetkisi yok ({person_role})")
            return {
                "status": "access_denied_role",
                "person": person_name,
                "role": person_role,
                "message": f"'{person_role}' rolünün '{room_name}' odasına erişim yetkisi yok."
            }

        # 4. Duygu analizi
        analysis = DeepFace.analyze(
            img_path=temp_file_path,
            actions=['emotion'],
            enforce_detection=False
        )
        if isinstance(analysis, list):
            analysis = analysis[0]

        raw_emotions = analysis.get('emotion', {})
        emotion_results = {k: float(v) for k, v in raw_emotions.items()}
        dominant_emotion = analysis.get('dominant_emotion', 'unknown')

        # 5. Gerekli duygu kontrolü
        rooms = get_rooms()
        required_emotion = rooms.get(room_name, "happy")

        if dominant_emotion.lower() != required_emotion.lower():
            write_log("Giriş Denemesi", person=person_name, room=room_name, result="Reddedildi",
                      detail=f"Duygu uyuşmadı (tespit: {dominant_emotion}, gerekli: {required_emotion})")
            return {
                "status": "access_denied_emotion",
                "person": person_name,
                "role": person_role,
                "dominant_emotion": dominant_emotion,
                "required_emotion": required_emotion,
                "emotions": emotion_results,
                "message": f"Gerekli duygu: '{required_emotion}', Tespit edilen: '{dominant_emotion}'"
            }

        # 6. ERİŞİM VERİLDİ - logla
        write_log("Giriş Yapıldı", person=person_name, room=room_name, result="Onaylandı",
                  detail=f"Rol: {person_role}, Duygu: {dominant_emotion}")
        db_record = models.AnalysisHistory(
            person_name=person_name,
            dominant_emotion=dominant_emotion
        )
        db.add(db_record)
        db.commit()

        return {
            "status": "access_granted",
            "person": person_name,
            "role": person_role,
            "room": room_name,
            "dominant_emotion": dominant_emotion,
            "emotions": emotion_results,
            "message": f"{person_name} '{room_name}' odasına giriş yaptı."
        }

    except Exception as e:
        print(f"[access/check] Hata: {e}")
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        if os.path.exists(temp_file_path):
            os.remove(temp_file_path)


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
