import { AgentRequest, AgentResponse } from './types';

const API_BASE = import.meta.env.VITE_API_URL || '';

export async function askAgent(request: AgentRequest): Promise<AgentResponse> {
  const res = await fetch(`${API_BASE}/api/agent/ask`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Request failed with status ${res.status}`);
  }

  return res.json();
}
