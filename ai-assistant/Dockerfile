# syntax=docker/dockerfile:1
FROM python:3.12-slim
WORKDIR /app

COPY ai-assistant/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY ai-assistant/app ./app

EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
