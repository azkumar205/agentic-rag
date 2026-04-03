import json
import os
import sys
import time
import requests
import pandas as pd
from ragas import evaluate
from ragas.metrics import faithfulness, answer_relevancy, context_precision, context_recall

API_URL = os.environ.get("API_URL", "http://localhost:5000/api/agent/ask")
GOLDEN_PATH = os.environ.get("GOLDEN_SET", "golden_test_set.json")

with open(GOLDEN_PATH, "r", encoding="utf-8") as f:
    golden = json.load(f)

results = []
latencies = []
costs = []
intent_predictions = []
intent_actuals = []

for item in golden:
    q = item["question"]
    expected = item["expected_answer"]
    rel_chunks = item["relevant_chunks"]
    expected_intent = item.get("intent", "Unknown")

    start = time.time()
    resp = requests.post(API_URL, json={"question": q})
    latency_ms = (time.time() - start) * 1000
    latencies.append(latency_ms)

    if resp.status_code != 200:
        print(f"❌ API error for: {q}")
        sys.exit(2)

    data = resp.json()
    answer = data.get("answer", "")
    context = [c.get("content", "") for c in data.get("textCitations", [])]
    model_used = data.get("modelUsed", "unknown")
    reflection_score = data.get("reflectionScore", 0)
    from_cache = data.get("fromCache", False)
    token_usage = data.get("tokenUsage", {})
    est_cost = token_usage.get("estimatedCost", 0)
    costs.append(est_cost)

    # Intent tracking for confusion matrix
    predicted_intent = data.get("intent", {}).get("intent", "Unknown") if data.get("intent") else "Unknown"
    intent_predictions.append(predicted_intent)
    intent_actuals.append(expected_intent)

    results.append({
        "question": q,
        "answer": answer,
        "contexts": context,
        "expected": expected,
        "relevant_chunks": rel_chunks,
        "model_used": model_used,
        "reflection_score": reflection_score,
        "from_cache": from_cache,
        "latency_ms": latency_ms,
        "estimated_cost": est_cost,
        "predicted_intent": predicted_intent,
        "expected_intent": expected_intent,
    })

df = pd.DataFrame(results)

# ── RAGAS Evaluation ──
ragas_df = pd.DataFrame({
    "question": df["question"],
    "answer": df["answer"],
    "contexts": df["contexts"],
    "ground_truth": df["expected"],
    "relevant_chunks": df["relevant_chunks"]
})

metrics = [faithfulness, answer_relevancy, context_precision, context_recall]
report = evaluate(ragas_df, metrics)

print("\n" + "=" * 60)
print("RAGAS Evaluation Report:")
print("=" * 60)
print(report)

# ── Intent Confusion Matrix ──
print("\n" + "=" * 60)
print("Intent Classification Accuracy:")
print("=" * 60)
correct = sum(1 for a, p in zip(intent_actuals, intent_predictions) if a == p)
total = len(intent_actuals)
print(f"Accuracy: {correct}/{total} ({correct/total*100:.1f}%)")
print(f"\n{'Expected':<25} {'Predicted':<25} {'Match'}")
print("-" * 60)
for a, p in zip(intent_actuals, intent_predictions):
    match = "✅" if a == p else "❌"
    print(f"{a:<25} {p:<25} {match}")

# ── Latency & Cost Report ──
print("\n" + "=" * 60)
print("Latency & Cost Report:")
print("=" * 60)
print(f"Avg latency:  {sum(latencies)/len(latencies):.0f} ms")
print(f"P50 latency:  {sorted(latencies)[len(latencies)//2]:.0f} ms")
print(f"P95 latency:  {sorted(latencies)[int(len(latencies)*0.95)]:.0f} ms")
print(f"Max latency:  {max(latencies):.0f} ms")
print(f"Total cost:   ${sum(costs):.4f}")
print(f"Avg cost/req: ${sum(costs)/len(costs):.5f}")
cache_hits = sum(1 for r in results if r["from_cache"])
print(f"Cache hits:   {cache_hits}/{total} ({cache_hits/total*100:.1f}%)")

# ── Per-Question Detail ──
print("\n" + "=" * 60)
print("Per-Question Details:")
print("=" * 60)
for r in results:
    print(f"  Q: {r['question'][:60]}...")
    print(f"    Model: {r['model_used']} | Reflection: {r['reflection_score']}/10 | "
          f"Cache: {r['from_cache']} | {r['latency_ms']:.0f}ms")

# ── Threshold Checks ──
print("\n" + "=" * 60)
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
