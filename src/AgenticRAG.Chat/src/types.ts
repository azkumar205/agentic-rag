export interface AgentRequest {
  question: string;
  sessionId?: string;
  category?: string;
  topK?: number;
}

export interface AgentResponse {
  answer: string;
  textCitations: TextCitation[];
  imageCitations: ImageCitation[];
  toolsUsed: string[];
  reasoningSteps: string[];
  reflectionScore: number;
  fromCache: boolean;
  tokenUsage: TokenUsageInfo;
  sessionId: string;
}

export interface TextCitation {
  index: number;
  sourceDocument: string;
  content: string;
  relevanceScore: number;
  sourceType: string;
}

export interface ImageCitation {
  index: number;
  fileName: string;
  downloadUrl: string;
  description: string;
  sourceDocument: string;
  pageNumber: number;
}

export interface TokenUsageInfo {
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  estimatedCost: number;
  toolCallCount: number;
}
