import google.generativeai as genai
from config import GEMINI_API_KEY
import time

genai.configure(api_key=GEMINI_API_KEY)
models = ['gemini-2.0-flash', 'gemini-flash-latest', 'gemini-1.5-flash-latest', 'gemini-pro-latest']

for m in models:
    try:
        genai.GenerativeModel(m).generate_content('Test')
        print(f"{m}: OK")
    except Exception as e:
        print(f"{m}: FAILED - {getattr(e, 'message', str(e))}")
    time.sleep(1)
