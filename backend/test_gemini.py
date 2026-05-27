import traceback
from config import GEMINI_API_KEY
import google.generativeai as genai

try:
    genai.configure(api_key=GEMINI_API_KEY)
    model = genai.GenerativeModel('gemini-2.5-flash')
    response = model.generate_content('Test')
    print("BASARILI!")
except Exception as e:
    print(repr(e))
