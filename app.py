
from fastapi import FastAPI, Query, HTTPException
from pydantic import BaseModel
from typing import List, Optional
import base64
import io
from sentence_transformers import SentenceTransformer
import psycopg2
from pathlib import Path


# ============================================================
# CONFIGURATION
# ============================================================

# Create FastAPI application
app = FastAPI(
    title="QBMS Similarity API",
    description="API for detecting duplicate/similar questions",
    version="1.0"
)

# ------------------------------------------------------------
# Base directory
# ------------------------------------------------------------
# This allows the same code to work:
# 1. From PyCharm
# 2. From normal Python
# 3. From PyInstaller EXE
# ------------------------------------------------------------

BASE_DIR = Path(__file__).resolve().parent


# ------------------------------------------------------------
# Local Sentence Transformer model
# ------------------------------------------------------------

MODEL_PATH = r"C:\DATA\Duplicate Check\model\all-MiniLM-L6-v2"

print("Loading Sentence Transformer model...")
print("Model path:", MODEL_PATH)

model = SentenceTransformer(str(MODEL_PATH))

print("Model loaded successfully.")


# ------------------------------------------------------------
# Similarity threshold
# ------------------------------------------------------------

THRESHOLD = 0.85


# ============================================================
# POSTGRESQL CONFIGURATION
# ============================================================

DB_HOST = "localhost"
DB_PORT = 5432
DB_NAME = "qbms1"
DB_USER = "qbms_app"
DB_PASSWORD = "Admin@1357"


# ============================================================
# DATABASE CONNECTION
# ============================================================

def get_db_connection():

    conn = psycopg2.connect(
        host="localhost",
        port=5432,
        database="qbms1",
        user="qbms_app",
        password="Admin@1357"
    )

    return conn


# ============================================================
# ROOT ENDPOINT
# ============================================================

@app.get("/")
def home():

    return {
        "application": "QBMS Similarity API",
        "status": "running",
        "model": "all-MiniLM-L6-v2",
        "threshold": THRESHOLD
    }


# ============================================================
# SIMILARITY ENDPOINT
# ============================================================

@app.get("/similarity")
def similarity(
    question: str = Query(
        ...,
        description="Question to check for similarity"
    )
):

    # --------------------------------------------------------
    # Validate input
    # --------------------------------------------------------

    if not question.strip():

        return {
            "status": "error",
            "message": "Question cannot be empty."
        }


    # --------------------------------------------------------
    # Generate embedding for incoming question
    # --------------------------------------------------------

    new_embedding = model.encode(
        question,
        normalize_embeddings=True
    ).tolist()


    # --------------------------------------------------------
    # Convert embedding into PostgreSQL vector format
    # --------------------------------------------------------

    embedding_string = "[" + ",".join(
        str(x) for x in new_embedding
    ) + "]"


    # --------------------------------------------------------
    # Connect to PostgreSQL
    # --------------------------------------------------------

    conn = None

    try:

        conn = get_db_connection()

        cursor = conn.cursor()


        # ----------------------------------------------------
        # Find most similar question
        #
        # <=> is pgvector cosine distance.
        #
        # Similarity = 1 - cosine distance
        # ----------------------------------------------------

        # The question text is encrypted in the application's
        # "question" table, so only the id is returned here.
        query = """
            SELECT
            qe.question_id,
            ''::text AS question_text,
            1 - (qe.embedding <=> %s::vector) AS similarity
        FROM question_embeddings qe
        ORDER BY qe.embedding <=> %s::vector
        LIMIT 10;
        """


        cursor.execute(
            query,
            (
                embedding_string,
                embedding_string
            )
        )


        result = cursor.fetchone()


        # ----------------------------------------------------
        # No questions available in database
        # ----------------------------------------------------

        if result is None:

            cursor.close()

            return {
                "status": "success",
                "input_question": question,
                "similarity_found": False,
                "message": "No questions found in database."
            }


        # ----------------------------------------------------
        # Extract database result
        # ----------------------------------------------------

        question_id = result[0]
        stored_question = result[1]
        distance = float(result[2])


        # ----------------------------------------------------
        # Convert cosine distance to cosine similarity
        # ----------------------------------------------------

        similarity_score = 1 - distance


        # ----------------------------------------------------
        # Check threshold
        # ----------------------------------------------------

        is_duplicate = similarity_score >= THRESHOLD


        cursor.close()


        # ----------------------------------------------------
        # Return result
        # ----------------------------------------------------

        return {

            "status": "success",

            "input_question": question,

            "similar_question": stored_question,

            "question_id": question_id,

            "similarity_score": round(
                similarity_score,
                4
            ),

            "threshold": THRESHOLD,

            "duplicate": is_duplicate
        }


    # --------------------------------------------------------
    # Database / API error
    # --------------------------------------------------------

    except Exception as e:

        return {

            "status": "error",

            "message": str(e)
        }


    finally:

        if conn is not None:

            conn.close()


# ============================================================
# TEXT + IMAGE -> EMBEDDING (used by every endpoint below)
# ============================================================
#
# The ASP.NET application sends PLAIN text (it decrypts the
# question table itself) and, optionally, pictures as base64.
# Pictures are read with an offline OCR engine and the text found
# is added to the question text before the embedding is created.
#
#   pip install rapidocr-onnxruntime pillow numpy
#
# If the OCR package is not installed, pictures are ignored.

_ocr_engine = None
_ocr_failed = False


def get_ocr():

    global _ocr_engine, _ocr_failed

    if _ocr_engine is None and not _ocr_failed:

        try:
            from rapidocr_onnxruntime import RapidOCR
            _ocr_engine = RapidOCR()
        except Exception as e:
            print("OCR not available, pictures will be ignored:", e)
            _ocr_failed = True

    return _ocr_engine


def ocr_image(image_base64: str) -> str:

    engine = get_ocr()

    if engine is None or not image_base64:
        return ""

    try:
        from PIL import Image
        import numpy as np

        if "," in image_base64[:100]:
            image_base64 = image_base64.split(",", 1)[1]

        image = Image.open(
            io.BytesIO(base64.b64decode(image_base64))
        ).convert("RGB")

        result, _ = engine(np.array(image))

        if not result:
            return ""

        return " ".join(str(line[1]) for line in result)

    except Exception as e:
        print("OCR failed for one picture:", e)
        return ""


def build_text(question: str, images: Optional[List[str]]) -> str:

    parts = [(question or "").strip()]

    for image in (images or [])[:3]:
        text = ocr_image(image)
        if text:
            parts.append(text)

    return " ".join(p for p in parts if p).strip()


def to_vector(embedding) -> str:

    return "[" + ",".join(str(float(x)) for x in embedding) + "]"


class ContentItem(BaseModel):

    question: str = ""
    images_base64: Optional[List[str]] = None


class IndexItem(ContentItem):

    question_id: int


class DuplicateCheckRequest(ContentItem):

    pass


class CompareBatchRequest(BaseModel):

    items: List[ContentItem]


class IndexBatchRequest(BaseModel):

    items: List[IndexItem]


# ============================================================
# DUPLICATE CHECK ENDPOINT (used by the ASP.NET Setter upload)
# ============================================================
#
# POST /check-duplicate
#   { "question": "...", "images_base64": ["..."] }
#
# Returns the TOP 3 most similar indexed questions as
# question_id + similarity percentage. The ASP.NET application
# looks the question text up itself and decides (using its own
# configured percentage) whether the upload is a duplicate.

@app.post("/check-duplicate")
def check_duplicate(request: DuplicateCheckRequest):

    text = build_text(request.question, request.images_base64)

    # Nothing to compare (e.g. empty question, unreadable picture)
    if not text:
        return {"status": "success", "matches": []}

    embedding_string = to_vector(
        model.encode(text, normalize_embeddings=True)
    )

    conn = None

    try:

        conn = get_db_connection()

        cursor = conn.cursor()

        cursor.execute(
            """
            SELECT
                qe.question_id,
                1 - (qe.embedding <=> %s::vector) AS similarity
            FROM question_embeddings qe
            ORDER BY qe.embedding <=> %s::vector
            LIMIT 3;
            """,
            (embedding_string, embedding_string)
        )

        rows = cursor.fetchall()

        cursor.close()

        matches = [
            {
                "question_id": question_id,
                "similarity_percent": round(max(0.0, float(score)) * 100, 2)
            }
            for question_id, score in rows
        ]

        return {"status": "success", "matches": matches}

    except Exception as e:

        # A non-200 response tells the ASP.NET worker to retry later
        # instead of treating the question as "no duplicate found".
        raise HTTPException(status_code=500, detail=str(e))

    finally:

        if conn is not None:
            conn.close()


# ============================================================
# DUPLICATES INSIDE ONE UPLOADED FILE
# ============================================================
#
# POST /compare-batch   { "items": [ {question, images_base64}, ... ] }
#
# For every item returns its (up to) 3 most similar OTHER items
# of the same request: [{ "index": 4, "similarity_percent": 91.2 }]

@app.post("/compare-batch")
def compare_batch(request: CompareBatchRequest):

    import numpy as np

    texts = [
        build_text(item.question, item.images_base64)
        for item in request.items
    ]

    usable = [i for i, text in enumerate(texts) if text]

    result = [[] for _ in texts]

    if len(usable) < 2:
        return {"status": "success", "matches": result}

    vectors = model.encode(
        [texts[i] for i in usable],
        normalize_embeddings=True
    )

    scores = np.matmul(vectors, vectors.T)

    for a, original_index in enumerate(usable):

        order = np.argsort(-scores[a])

        for b in order:

            if b == a:
                continue

            if len(result[original_index]) >= 3:
                break

            result[original_index].append({
                "index": usable[int(b)],
                "similarity_percent": round(
                    max(0.0, float(scores[a][b])) * 100, 2
                )
            })

    return {"status": "success", "matches": result}


# ============================================================
# INDEXING (keeps question_embeddings in step with the app)
# ============================================================
#
# POST /index-batch
#   { "items": [ {question_id, question, images_base64}, ... ] }
#
# Called by the ASP.NET worker for every saved question that has
# no embedding yet (new uploads, manually created questions and
# older questions). Returns the ids that were indexed.

@app.post("/index-batch")
def index_batch(request: IndexBatchRequest):

    texts = []
    ids = []

    for item in request.items:

        text = build_text(item.question, item.images_base64)

        if text:
            ids.append(item.question_id)
            texts.append(text)

    if not ids:
        return {"status": "success", "indexed": []}

    vectors = model.encode(texts, normalize_embeddings=True)

    conn = None

    try:

        conn = get_db_connection()

        cursor = conn.cursor()

        for question_id, vector in zip(ids, vectors):

            cursor.execute(
                """
                INSERT INTO question_embeddings (question_id, embedding)
                VALUES (%s, %s::vector)
                ON CONFLICT (question_id)
                DO UPDATE SET embedding = EXCLUDED.embedding;
                """,
                (question_id, to_vector(vector))
            )

        conn.commit()

        cursor.close()

        return {"status": "success", "indexed": ids}

    except Exception as e:

        if conn is not None:
            conn.rollback()

        raise HTTPException(status_code=500, detail=str(e))

    finally:

        if conn is not None:
            conn.close()


# ============================================================
# APPLICATION START
# ============================================================

if __name__ == "__main__":

    import uvicorn

    uvicorn.run(
        app,
        host="0.0.0.0",
        port=8000
    )

