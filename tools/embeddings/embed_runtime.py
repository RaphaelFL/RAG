import json
import os
import shutil
import sys


def normalize_vector(vector):
    norm = sum(value * value for value in vector) ** 0.5
    if norm == 0:
        return vector
    return [float(value / norm) for value in vector]


def load_model(model_name, model_path):
    try:
        from sentence_transformers import SentenceTransformer
    except Exception as exc:
        raise RuntimeError(
            "sentence-transformers nao esta disponivel no ambiente Python. "
            "Instale com: pip install sentence-transformers torch"
        ) from exc

    cache_folder = None
    if model_path:
        cache_folder = model_path
        os.makedirs(cache_folder, exist_ok=True)

        if os.listdir(cache_folder):
            try:
                return SentenceTransformer(cache_folder)
            except Exception:
                shutil.rmtree(cache_folder, ignore_errors=True)
                os.makedirs(cache_folder, exist_ok=True)

    if not model_name:
        raise RuntimeError("Nenhum model_name ou model_path foi fornecido para o runtime interno de embeddings.")

    return SentenceTransformer(model_name, cache_folder=cache_folder)


def main():
    payload = json.load(sys.stdin)
    model = load_model(payload.get("modelName", ""), payload.get("modelPath", ""))
    texts = payload.get("texts", [])

    if not texts:
        raise RuntimeError("Nenhum texto foi enviado para gerar embeddings.")

    vectors = model.encode(texts, convert_to_numpy=True, show_progress_bar=False)
    normalize = bool(payload.get("normalizeVectors", True))

    result_vectors = []
    for vector in vectors:
        values = [float(value) for value in vector.tolist()]
        result_vectors.append(normalize_vector(values) if normalize else values)

    dimensions = len(result_vectors[0]) if result_vectors else 0

    json.dump(
        {
            "modelName": payload.get("modelName", ""),
            "modelVersion": payload.get("modelVersion", ""),
            "dimensions": dimensions,
            "vectors": result_vectors,
        },
        sys.stdout,
    )


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(str(exc), file=sys.stderr)
        sys.exit(1)