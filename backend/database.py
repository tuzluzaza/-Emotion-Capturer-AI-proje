from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker
from sqlalchemy.ext.declarative import declarative_base

# SQLite veritabanı dosyası
SQLALCHEMY_DATABASE_URL = "sqlite:///./face_emotion_app.db"

# connect_args={"check_same_thread": False} sadece SQLite için gereklidir.
engine = create_engine(
    SQLALCHEMY_DATABASE_URL, connect_args={"check_same_thread": False}
)

# Veritabanı oturumlarını (session) oluşturmak için sınıf
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

# Modellerimizin miras alacağı temel sınıf
Base = declarative_base()

# Veritabanı oturumu almak için dependency
def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
