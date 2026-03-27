import { useState, useRef, useEffect, useCallback } from 'react';
import ReactMarkdown from 'react-markdown';
import { askAgent } from './api';
import { AgentResponse } from './types';
import './App.css';

interface ChatMessage {
  role: 'user' | 'bot';
  content: string;
  response?: AgentResponse;
  error?: string;
}

const SUGGESTIONS = [
  'What are the key contract terms?',
  'Show me the billing overview',
  'Summarize the vendor analysis',
  'What documents are available?',
];

export default function App() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [sessionId, setSessionId] = useState<string | undefined>();
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  useEffect(scrollToBottom, [messages, loading, scrollToBottom]);

  const sendMessage = async (text: string) => {
    if (!text.trim() || loading) return;

    const userMsg: ChatMessage = { role: 'user', content: text.trim() };
    setMessages((prev) => [...prev, userMsg]);
    setInput('');
    setLoading(true);

    // Auto-resize textarea back
    if (textareaRef.current) textareaRef.current.style.height = '44px';

    try {
      const response = await askAgent({
        question: text.trim(),
        sessionId,
      });

      setSessionId(response.sessionId);

      const botMsg: ChatMessage = {
        role: 'bot',
        content: response.answer,
        response,
      };
      setMessages((prev) => [...prev, botMsg]);
    } catch (err) {
      const errorMsg: ChatMessage = {
        role: 'bot',
        content: '',
        error: err instanceof Error ? err.message : 'Something went wrong.',
      };
      setMessages((prev) => [...prev, errorMsg]);
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    sendMessage(input);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage(input);
    }
  };

  const handleTextareaChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setInput(e.target.value);
    e.target.style.height = '44px';
    e.target.style.height = Math.min(e.target.scrollHeight, 120) + 'px';
  };

  const sanitizeFileName = (name: string) =>
    name.replace(/[^a-z0-9._-]/gi, '_').slice(0, 120) || 'citation';

  const downloadTextCitation = (
    sourceDocument: string,
    content: string,
    index: number,
  ) => {
    const safeName = sanitizeFileName(sourceDocument || `citation_${index}`);
    const payload = [
      `Source: ${sourceDocument || 'Unknown'}`,
      `Citation: ${index}`,
      '',
      content || '(No citation content returned by API)',
    ].join('\n');

    const blob = new Blob([payload], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${safeName}_citation_${index}.txt`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <div className="app">
      {/* Header */}
      <header className="header">
        <div className="header-icon">🤖</div>
        <div>
          <h1>Agentic RAG Chat</h1>
          <div className="subtitle">AI-powered document intelligence</div>
        </div>
      </header>

      {/* Messages */}
      <div className="messages">
        {messages.length === 0 && !loading && (
          <div className="welcome">
            <div className="welcome-icon">🧠</div>
            <h2>Welcome to Agentic RAG</h2>
            <p>
              Ask questions about your documents, billing data, contracts, and
              more. I use multiple tools to find the best answers.
            </p>
            <div className="suggestions">
              {SUGGESTIONS.map((s) => (
                <button
                  key={s}
                  className="suggestion-btn"
                  onClick={() => sendMessage(s)}
                >
                  {s}
                </button>
              ))}
            </div>
          </div>
        )}

        {messages.map((msg, i) => {
          if (msg.error) {
            return (
              <div key={i} className="error-msg">
                ⚠️ {msg.error}
              </div>
            );
          }

          return (
            <div key={i} className={`message-row ${msg.role}`}>
              <div className={`avatar ${msg.role}`}>
                {msg.role === 'user' ? '👤' : '🤖'}
              </div>
              <div className={`bubble ${msg.role}`}>
                {msg.role === 'user' ? (
                  msg.content
                ) : (
                  <>
                    <ReactMarkdown>{msg.content}</ReactMarkdown>

                    {/* Text Citations */}
                    {msg.response &&
                      msg.response.textCitations.length > 0 && (
                        <div className="citations">
                          <div className="citations-title">📄 Sources</div>
                          {msg.response.textCitations.map((c) => (
                            <div key={c.index} className="citation-item">
                              <div className="citation-main">
                                <span className="citation-index">{c.index}</span>
                                <span className="citation-name">{c.sourceDocument}</span>
                              </div>
                              <button
                                type="button"
                                className="citation-download-btn"
                                onClick={() =>
                                  downloadTextCitation(
                                    c.sourceDocument,
                                    c.content,
                                    c.index,
                                  )
                                }
                              >
                                Download
                              </button>
                            </div>
                          ))}
                        </div>
                      )}

                    {/* Image Citations */}
                    {msg.response &&
                      msg.response.imageCitations.length > 0 && (
                        <div className="image-citations">
                          {msg.response.imageCitations.map((img) => (
                            <div key={img.index} className="image-citation">
                              <img src={img.downloadUrl} alt={img.description} />
                              <div className="image-caption">
                                {img.description || img.fileName}
                              </div>
                              <a
                                className="image-download-link"
                                href={img.downloadUrl}
                                target="_blank"
                                rel="noreferrer"
                                download={img.fileName || `image-citation-${img.index}`}
                              >
                                Download
                              </a>
                            </div>
                          ))}
                        </div>
                      )}

                    {/* Metadata tags */}
                    {msg.response && (
                      <div className="meta">
                        {msg.response.toolsUsed.map((t) => (
                          <span key={t} className="meta-tag">
                            🔧 {t}
                          </span>
                        ))}
                        {msg.response.fromCache && (
                          <span className="meta-tag cache">⚡ Cached</span>
                        )}
                        <span className="meta-tag">
                          🎯 Score: {msg.response.reflectionScore}/10
                        </span>
                        <span className="meta-tag">
                          🪙 {msg.response.tokenUsage.totalTokens} tokens
                        </span>
                      </div>
                    )}
                  </>
                )}
              </div>
            </div>
          );
        })}

        {loading && (
          <div className="message-row bot">
            <div className="avatar bot">🤖</div>
            <div className="bubble bot">
              <div className="typing">
                <span />
                <span />
                <span />
              </div>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input */}
      <div className="input-area">
        <form className="input-form" onSubmit={handleSubmit}>
          <textarea
            ref={textareaRef}
            value={input}
            onChange={handleTextareaChange}
            onKeyDown={handleKeyDown}
            placeholder="Ask a question about your documents..."
            rows={1}
            disabled={loading}
          />
          <button
            type="submit"
            className="send-btn"
            disabled={loading || !input.trim()}
            title="Send"
          >
            ➤
          </button>
        </form>
      </div>
    </div>
  );
}
