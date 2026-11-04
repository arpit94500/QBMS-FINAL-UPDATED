from sentence_transformers import SentenceTransformer
import psycopg2

# -----------------------------------------
# 1. Load embedding model
# -----------------------------------------

model = SentenceTransformer("all-MiniLM-L6-v2")

# -----------------------------------------
# 2. Connect to PostgreSQL
# -----------------------------------------

conn = psycopg2.connect(
    host="localhost",
    port=5432,
    database="qbms1",
    user="qbms_app",
    password="Admin@1357"
)

cursor = conn.cursor()

# -----------------------------------------
# 3. Fetch ALL questions from database
# -----------------------------------------

cursor.execute("""
    SELECT question_id, question_text
    FROM questions
    ORDER BY question_id;
""")

questions = cursor.fetchall()

print(f"Total questions found: {len(questions)}")

# -----------------------------------------
# 4. Generate and store embeddings
# -----------------------------------------

for question_id, question_text in questions:

    print(f"Processing question ID: {question_id}")

    # Generate embedding
    embedding = model.encode(question_text).tolist()

    # Insert embedding
    cursor.execute("""
        INSERT INTO question_embeddings
            (question_id, embedding)
        VALUES
            (%s, %s::vector)
        ON CONFLICT (question_id)
        DO UPDATE SET
            embedding = EXCLUDED.embedding;
    """, (question_id, embedding))

# -----------------------------------------
# 5. Save changes
# -----------------------------------------

conn.commit()

print("All embeddings generated and stored successfully.")

# -----------------------------------------
# 6. Close database connection
# -----------------------------------------

cursor.close()
conn.close()
