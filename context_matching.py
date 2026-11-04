from fastapi import FastAPI
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer
import psycopg2

app = FastAPI()

# Load model only once when the API starts
model = SentenceTransformer("all-MiniLM-L6-v2")

# Temporary threshold
THRESHOLD = 0.85


class QuestionRequest(BaseModel):
    question: str


@app.post("/check")
def check_duplicate(request: QuestionRequest):

    # Generate embedding for incoming question
    embedding = model.encode(request.question).tolist()

    # Connect to PostgreSQL
    conn = psycopg2.connect(
        host="localhost",
        port=5432,
        database="qbms1",
        user="qbms_app",
        password="Admin@1357"
    )

    cursor = conn.cursor()

    # Find most similar questions
    cursor.execute(
        """
        SELECT
            q.question_id,
            q.question_text,
            1 - (qe.embedding <=> %s::vector) AS similarity
        FROM question_embeddings qe
        JOIN questions q
            ON q.question_id = qe.question_id
        ORDER BY qe.embedding <=> %s::vector
        LIMIT 10;
        """,
        (embedding, embedding)
    )

    results = cursor.fetchall()

    cursor.close()
    conn.close()

    matches = []

    for question_id, question_text, similarity in results:

        matches.append({
            "question_id": question_id,
            "question_text": question_text,
            "similarity": round(float(similarity), 4)
        })

    # Determine duplicate based on highest similarity
    is_duplicate = False

    if matches and matches[0]["similarity"] >= THRESHOLD:
        is_duplicate = True

    return {
        "question": request.question,
        "is_duplicate": is_duplicate,
        "threshold": THRESHOLD,
        "matches": matches
    }
