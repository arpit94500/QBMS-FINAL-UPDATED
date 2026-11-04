from fastapi import FastAPI, Query
from transformers import AutoTokenizer
import onnxruntime as ort
import numpy as np
import psycopg2

app = FastAPI()
# -----------------------\.venv\Scripts\uvicorn.exe app.main:app --host 0.0.0.0 --port 8000\.venv\Scripts\uvicorn.exe app.main:app --host 0.0.0.0 --port 8000---------------------------
# LOCAL MODEL PATH
# --------------------------------------------------
MODEL_PATH = r"C:\DATA\Duplicate Check\model\all-MiniLM-L6-v2"

# ONNX model file inside the model folder
ONNX_MODEL = r"C:\DATA\Duplicate Check\model\all-MiniLM-L6-v2-onnx\model.onnx"

# --------------------------------------------------
# SETTINGS
# --------------------------------------------------
THRESHOLD = 0.85

# --------------------------------------------------
# LOAD TOKENIZER
# --------------------------------------------------
tokenizer = AutoTokenizer.from_pretrained(
    MODEL_PATH,
    local_files_only=True
)

# --------------------------------------------------
# LOAD ONNX MODEL
# --------------------------------------------------
session = ort.InferenceSession(
    ONNX_MODEL,
    providers=["CPUExecutionProvider"]
)


# --------------------------------------------------
# CREATE EMBEDDING
# --------------------------------------------------
def create_embedding(text: str):

    encoded = tokenizer(
        text,
        padding=True,
        truncation=True,
        max_length=256,
        return_tensors="np"
    )

    input_ids = encoded["input_ids"]
    attention_mask = encoded["attention_mask"]

    # Some MiniLM ONNX models also require token_type_ids
    inputs = {
        "input_ids": input_ids,
        "attention_mask": attention_mask
    }

    input_names = [x.name for x in session.get_inputs()]

    if "token_type_ids" in input_names:
        inputs["token_type_ids"] = np.zeros_like(input_ids)

    outputs = session.run(None, inputs)

    # First output = token embeddings
    token_embeddings = outputs[0]

    # Mean pooling
    attention_mask_expanded = np.expand_dims(
        attention_mask,
        axis=-1
    )

    attention_mask_expanded = np.broadcast_to(
        attention_mask_expanded,
        token_embeddings.shape
    )

    sum_embeddings = np.sum(
        token_embeddings * attention_mask_expanded,
        axis=1
    )

    sum_mask = np.clip(
        attention_mask_expanded.sum(axis=1),
        a_min=1e-9,
        a_max=None
    )

    embedding = sum_embeddings / sum_mask

    # Normalize embedding
    embedding = embedding / np.linalg.norm(
        embedding,
        axis=1,
        keepdims=True
    )

    return embedding[0].tolist()


# --------------------------------------------------
# SIMILARITY API
# --------------------------------------------------
@app.get("/similarity")
def similarity(
    question: str = Query(...)
):

    # Generate embedding for incoming question
    new_embedding = create_embedding(question)

    # Connect to PostgreSQL
    conn = psycopg2.connect(
        host="localhost",
        port=5432,
        database="qbms1",
        user="qbms_app",
        password="Admin@1357"
    )

    cursor = conn.cursor()

    # Find top 10 similar questions
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
        (new_embedding, new_embedding)
    )

    rows = cursor.fetchall()

    cursor.close()
    conn.close()

    # Prepare response
    results = []

    for question_id, question_text, similarity_score in rows:

        similarity_score = float(similarity_score)

        results.append({
            "question_id": question_id,
            "question": question_text,
            "similarity": round(similarity_score, 4),
            "duplicate": similarity_score >= THRESHOLD
        })

    # Overall duplicate status
    is_duplicate = False

    if results:
        is_duplicate = results[0]["similarity"] >= THRESHOLD

    return {
        "input_question": question,
        "threshold": THRESHOLD,
        "is_duplicate": is_duplicate,
        "results": results
    }
