import * as React from "react"
import { ChatbotIcon, type ChatbotIconVariant } from "./brand/chatbot-icon"
import { X, Send, Sparkles, ChevronDown } from "lucide-react"
import type { ChatMessage } from "./assistant/types"
import { RecommendationCard } from "./assistant/recommendation-card"
import { sendAssistantMessage } from "@/lib/assistant"

type WidgetMessage = ChatMessage & {
  id: string
  time: string
}

const INITIAL_MESSAGES: WidgetMessage[] = [
  {
    id: "1",
    sender: "bot",
    text: "Hey there! I'm FuelBot, your OSSFuel discovery assistant. Ask me to find open source projects, explore libraries, or find projects to fund!",
    time: "Just now",
  },
]

const QUICK_PROMPTS = [
  "Find AI developer tools with active maintenance",
  "Tell me about OSSFuel",
  "TypeScript developer libraries",
]

let messageCounter = 0

function generateMessageId(): string {
  messageCounter += 1
  return `msg-${Date.now()}-${messageCounter}`
}

function formatCurrentTime(): string {
  return new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
}

export function AssistantWidget() {
  const [isOpen, setIsOpen] = React.useState(false)
  const variant: ChatbotIconVariant = "buddy"
  const [messages, setMessages] = React.useState<WidgetMessage[]>(INITIAL_MESSAGES)
  const [input, setInput] = React.useState("")
  const [isTyping, setIsTyping] = React.useState(false)
  const messagesEndRef = React.useRef<HTMLDivElement>(null)

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" })
  }

  React.useEffect(() => {
    if (isOpen) {
      scrollToBottom()
    }
  }, [isOpen, messages, isTyping])

  const handleSend = async (textToSend?: string) => {
    const text = (textToSend ?? input).trim()
    if (!text || isTyping) return

    const userMsg: WidgetMessage = {
      id: generateMessageId(),
      sender: "user",
      text,
      time: formatCurrentTime(),
    }

    setMessages((prev) => [...prev, userMsg])
    if (!textToSend) setInput("")
    setIsTyping(true)

    try {
      const history = [...messages, userMsg].map((m) => ({
        sender: m.sender,
        text: m.text,
      }))

      const response = await sendAssistantMessage({
        messages: history,
      })

      const botMsg: WidgetMessage = {
        id: generateMessageId(),
        sender: "bot",
        text: response.reply,
        recommendations: response.recommendations,
        time: formatCurrentTime(),
      }

      setMessages((prev) => [...prev, botMsg])
    } catch (error) {
      console.error("Assistant chat error:", error)
      const errorMsg: WidgetMessage = {
        id: generateMessageId(),
        sender: "bot",
        text: "I couldn't reach the assistant server right now. Please verify your connection or try again shortly.",
        time: formatCurrentTime(),
      }
      setMessages((prev) => [...prev, errorMsg])
    } finally {
      setIsTyping(false)
    }
  }

  return (
    <div className="fixed bottom-5 right-5 z-50 flex flex-col items-end">
      {/* Expanded Assistant Box */}
      {isOpen && (
        <div
          role="dialog"
          aria-label="OSSFuel Assistant"
          className="mb-3 flex h-[540px] w-[350px] sm:w-[410px] flex-col overflow-hidden rounded-2xl border border-border bg-card text-card-foreground shadow-2xl transition-all duration-200 animate-in fade-in slide-in-from-bottom-4"
        >
          {/* Header */}
          <div className="flex items-center justify-between border-b border-border bg-muted/40 px-4 py-3">
            <div className="flex items-center gap-2.5">
              <div className="relative flex h-9 w-9 items-center justify-center rounded-xl bg-background border border-border shadow-xs">
                <ChatbotIcon variant={variant} size={28} />
                <span className="absolute -top-0.5 -right-0.5 flex h-2.5 w-2.5">
                  <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
                  <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-emerald-500" />
                </span>
              </div>
              <div>
                <div className="flex items-center gap-1.5 font-semibold text-sm leading-none">
                  <span>Assistant</span>
                  <span className="rounded-full bg-primary/10 px-1.5 py-0.5 text-[10px] font-medium text-primary">
                    AI
                  </span>
                </div>
                <span className="text-[11px] text-muted-foreground">Discovery Copilot</span>
              </div>
            </div>

            <button
              onClick={() => setIsOpen(false)}
              className="rounded-lg p-1.5 text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
              aria-label="Close assistant"
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          {/* Messages Area */}
          <div className="flex-1 overflow-y-auto p-3.5 space-y-3 text-sm">
            {messages.map((m) => (
              <div
                key={m.id}
                className={`flex gap-2.5 ${
                  m.sender === "user" ? "justify-end" : "justify-start"
                }`}
              >
                {m.sender === "bot" && (
                  <div className="h-7 w-7 shrink-0 rounded-lg bg-muted flex items-center justify-center border border-border/80 mt-0.5">
                    <ChatbotIcon variant={variant} size={20} />
                  </div>
                )}
                <div
                  className={`rounded-2xl px-3.5 py-2.5 leading-relaxed text-xs sm:text-sm ${
                    m.sender === "user"
                      ? "max-w-[80%] bg-primary text-primary-foreground rounded-br-xs"
                      : "w-full max-w-[88%] sm:max-w-[90%] bg-muted/60 text-foreground border border-border/60 rounded-bl-xs"
                  }`}
                >
                  <p className="whitespace-pre-wrap">{m.text}</p>

                  {m.recommendations && m.recommendations.length > 0 && (
                    <div className="mt-2.5 flex flex-col gap-2 w-full">
                      {m.recommendations.map((card, idx) => (
                        <RecommendationCard
                          key={`${card.projectId}-${idx}`}
                          card={card}
                        />
                      ))}
                    </div>
                  )}
                </div>
              </div>
            ))}

            {isTyping && (
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <div className="h-7 w-7 rounded-lg bg-muted flex items-center justify-center border border-border/80">
                  <ChatbotIcon variant={variant} size={20} />
                </div>
                <div className="flex items-center gap-1.5 bg-muted/60 px-3 py-2 rounded-2xl border border-border/60">
                  <span className="h-1.5 w-1.5 rounded-full bg-muted-foreground animate-bounce [animation-delay:-0.3s]" />
                  <span className="h-1.5 w-1.5 rounded-full bg-muted-foreground animate-bounce [animation-delay:-0.15s]" />
                  <span className="h-1.5 w-1.5 rounded-full bg-muted-foreground animate-bounce" />
                </div>
              </div>
            )}

            <div ref={messagesEndRef} />
          </div>

          {/* Quick Prompts */}
          <div className="border-t border-border/60 bg-muted/10 px-3 py-2">
            <div className="flex flex-wrap gap-1.5">
              {QUICK_PROMPTS.map((prompt) => (
                <button
                  key={prompt}
                  type="button"
                  disabled={isTyping}
                  onClick={() => handleSend(prompt)}
                  className="rounded-full border border-border bg-background px-2.5 py-1 text-[11px] text-muted-foreground hover:border-primary/50 hover:text-foreground transition-all text-left disabled:opacity-50"
                >
                  {prompt}
                </button>
              ))}
            </div>
          </div>

          {/* Input Box */}
          <form
            onSubmit={(e) => {
              e.preventDefault()
              handleSend()
            }}
            className="flex items-center gap-2 border-t border-border bg-background p-2.5"
          >
            <input
              type="text"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              disabled={isTyping}
              placeholder="Ask FuelBot to find projects..."
              className="flex-1 bg-transparent px-2.5 py-1.5 text-xs sm:text-sm text-foreground placeholder:text-muted-foreground focus:outline-none disabled:opacity-60"
            />
            <button
              type="submit"
              disabled={!input.trim() || isTyping}
              className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-primary-foreground disabled:opacity-40 hover:opacity-90 transition-opacity shrink-0"
              aria-label="Send message"
            >
              <Send className="h-3.5 w-3.5" />
            </button>
          </form>
        </div>
      )}

      {/* Floating Trigger Button */}
      <button
        onClick={() => setIsOpen((prev) => !prev)}
        className="group relative flex items-center gap-2.5 rounded-full border border-border bg-card/95 py-2 pl-2.5 pr-4 text-card-foreground shadow-lg backdrop-blur-md transition-all hover:border-primary/50 hover:shadow-xl active:scale-95"
        aria-expanded={isOpen}
        aria-label="Toggle Assistant"
      >
        <div className="relative flex h-9 w-9 items-center justify-center rounded-full bg-background border border-border/80 shadow-2xs group-hover:scale-105 transition-transform">
          <ChatbotIcon variant={variant} size={28} />
          <span className="absolute -top-0.5 -right-0.5 flex h-2.5 w-2.5">
            <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
            <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-emerald-500" />
          </span>
        </div>

        <div className="flex flex-col items-start text-left">
          <div className="flex items-center gap-1.5">
            <span className="text-xs font-semibold tracking-tight">Assistant</span>
            <span className="flex h-1.5 w-1.5 rounded-full bg-emerald-500" />
          </div>
          <span className="text-[10px] text-muted-foreground leading-none">Need help?</span>
        </div>

        {isOpen ? (
          <ChevronDown className="h-4 w-4 text-muted-foreground ml-1" />
        ) : (
          <Sparkles className="h-3.5 w-3.5 text-primary ml-1 group-hover:rotate-12 transition-transform" />
        )}
      </button>
    </div>
  )
}

export default AssistantWidget
