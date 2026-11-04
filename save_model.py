from sentence_transformers import SentenceTransformer

model = SentenceTransformer("all-MiniLM-L6-v2")

model.save(r"C:\DATA\Duplicate Check\model\offline_nlp_model")

print("Model saved successfully.")
