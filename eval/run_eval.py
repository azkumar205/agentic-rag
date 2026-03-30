import json
import os
import sys
import requests
import pandas as pd
from ragas import evaluate
from ragas.metrics import faithfulness, answer_relevancy, context_precision, context_recall

API_URL = os.environ.get("API_URL", "http://localhost:5000/api/agent/ask")
GOLDEN_PATH = os.environ.get("GOLDEN_SET", "golden_test_set.json")

with open(GOLDEN_PATH, "r", encoding="utf-8") as f:
    golden = json.load(f)

results = []
for item in golden:
    q = item["question"]
    expected = item["expected_answer"]
    rel_chunks = item["relevant_chunks"]
    resp = requests.post(API_URL, json={"question": q})
    if resp.status_code != 200:
        print(f"❌ API error for: {q}")
        sys.exit(2)
    data = resp.json()
    answer = data.get("answer", "")
    context = [c.get("content", "") for c in data.get("textCitations", [])]
    results.append({
        "question": q,
        "answer": answer,
        "contexts": context,
        "expected": expected,
        "relevant_chunks": rel_chunks
    })

df = pd.DataFrame(results)

# RAGAS expects: question, answer, contexts, ground_truth, relevant_chunks
ragas_df = pd.DataFrame({
    "question": df["question"],
    "answer": df["answer"],
    "contexts": df["contexts"],
    "ground_truth": df["expected"],
    "relevant_chunks": df["relevant_chunks"]
})

metrics = [faithfulness, answer_relevancy, context_precision, context_recall]
report = evaluate(ragas_df, metrics)

print("\nRAGAS Evaluation Report:")
print(report)

fail = False
if report["faithfulness"] < 0.80:
    print(f"❌ FAILED: faithfulness {report['faithfulness']:.2f} < 0.80")
    fail = True
if report["context_precision"] < 0.70:
    print(f"❌ FAILED: context_precision {report['context_precision']:.2f} < 0.70")
    fail = True
if report["context_recall"] < 0.70:
    print(f"❌ FAILED: context_recall {report['context_recall']:.2f} < 0.70")
    fail = True
if report["answer_relevancy"] < 0.70:
    print(f"❌ FAILED: answer_relevancy {report['answer_relevancy']:.2f} < 0.70")
    fail = True

if fail:
    sys.exit(1)
else:
    print("✅ PASSED: All metrics above threshold.")
    sys.exit(0)
