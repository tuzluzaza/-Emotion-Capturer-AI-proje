from sqlalchemy import Column, Integer, String, DateTime
from database import Base
from datetime import datetime

class AnalysisHistory(Base):
    __tablename__ = "analysis_history"

    id = Column(Integer, primary_key=True, index=True)
    date = Column(DateTime, default=datetime.utcnow)
    person_name = Column(String, index=True)
    dominant_emotion = Column(String)

    def __repr__(self):
        return f"<AnalysisHistory(person='{self.person_name}', emotion='{self.dominant_emotion}')>"
