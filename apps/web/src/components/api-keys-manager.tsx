import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Check, Copy, Key, ShieldAlert, Terminal, Trash2 } from "lucide-react"
import { Loader } from "@/components/loader"
import {
  apiKeysQueryOptions,
  createApiKey,
  revokeApiKey,
  type CreateApiKeyResponse,
} from "@/lib/gateway"
import {
  buttonOutlineCompact,
  buttonPrimaryCompact,
  eyebrowItem,
  panel,
} from "@/lib/styles"
import { cn } from "@workspace/ui/lib/utils"

export function ApiKeysManager({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient()
  const [keyName, setKeyName] = useState("")
  const [newKey, setNewKey] = useState<CreateApiKeyResponse | null>(null)
  const [copied, setCopied] = useState(false)

  const keysQuery = useQuery(apiKeysQueryOptions(projectId))

  const createMut = useMutation({
    mutationFn: (name: string) => createApiKey(projectId, name),
    onSuccess: (data) => {
      setNewKey(data)
      setKeyName("")
      queryClient.invalidateQueries({
        queryKey: ["projects", projectId, "api-keys"],
      })
    },
  })

  const revokeMut = useMutation({
    mutationFn: (keyId: string) => revokeApiKey(projectId, keyId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["projects", projectId, "api-keys"],
      })
    },
  })

  const copyToClipboard = async (text: string) => {
    await navigator.clipboard.writeText(text)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <section className="mt-8 border-t border-foreground/15 pt-8">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <span className={eyebrowItem}>Developer Integration</span>
          <h2 className="text-xl font-bold tracking-[-0.03em]">Project API Keys</h2>
          <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
            Generate project keys for coding tools like Aider, OpenCode, and Continue to consume this project&apos;s AI balance via the OpenAI-compatible gateway.
          </p>
        </div>
      </div>

      {newKey ? (
        <div className={cn(panel, "mt-4 border-amber-500/40 bg-amber-500/10")} role="alert">
          <div className="flex items-center gap-2 text-amber-500 font-semibold text-sm">
            <ShieldAlert size={16} />
            <span>Save your key now — you won&apos;t be able to see it again!</span>
          </div>
          <div className="mt-3 flex items-center gap-2">
            <code className="flex-1 rounded bg-background/80 px-3 py-2 font-mono text-xs select-all break-all">
              {newKey.rawKey}
            </code>
            <button
              type="button"
              onClick={() => copyToClipboard(newKey.rawKey)}
              className={cn(buttonPrimaryCompact, "shrink-0")}
            >
              {copied ? <Check size={14} /> : <Copy size={14} />}
              {copied ? "Copied" : "Copy"}
            </button>
          </div>
          <div className="mt-3 text-xs text-muted-foreground">
            Use with Aider / OpenCode: <code className="font-mono">OPENAI_API_BASE=https://api.ossfuel.louayhouimli.dev/v1 OPENAI_API_KEY={newKey.rawKey}</code>
          </div>
        </div>
      ) : null}

      <form
        onSubmit={(e) => {
          e.preventDefault()
          if (keyName.trim()) {
            createMut.mutate(keyName.trim())
          }
        }}
        className="mt-4 flex flex-col gap-2 sm:flex-row"
      >
        <input
          type="text"
          placeholder="Key name (e.g., Aider CLI on MacBook)"
          value={keyName}
          onChange={(e) => setKeyName(e.target.value)}
          disabled={createMut.isPending}
          className="flex-1 rounded-md border border-foreground/20 bg-background px-3 py-1.5 text-sm outline-none focus:border-foreground"
        />
        <button
          type="submit"
          disabled={createMut.isPending || !keyName.trim()}
          className={cn(buttonPrimaryCompact, "disabled:opacity-50")}
        >
          <Key size={14} />
          {createMut.isPending ? "Creating..." : "Generate key"}
        </button>
      </form>

      {createMut.isError ? (
        <p className="mt-2 text-xs font-semibold text-destructive">
          {(createMut.error as Error)?.message ?? "Failed to create API key."}
        </p>
      ) : null}

      <div className="mt-5 space-y-2">
        {keysQuery.isLoading ? (
          <Loader label="Loading API keys..." />
        ) : keysQuery.data && keysQuery.data.length > 0 ? (
          <div className="divide-y divide-foreground/10 rounded-md border border-foreground/15">
            {keysQuery.data.map((key) => (
              <div
                key={key.id}
                className="flex flex-col gap-2 p-3 sm:flex-row sm:items-center sm:justify-between"
              >
                <div>
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-sm">{key.name}</span>
                    <code className="rounded bg-muted px-1.5 py-0.5 font-mono text-xs text-muted-foreground">
                      {key.keyPrefix}...
                    </code>
                    {!key.isActive ? (
                      <span className="rounded bg-destructive/10 px-1.5 py-0.5 text-xs text-destructive">
                        Revoked
                      </span>
                    ) : (
                      <span className="rounded bg-emerald-500/10 px-1.5 py-0.5 text-xs text-emerald-500">
                        Active
                      </span>
                    )}
                  </div>
                  <div className="mt-1 flex items-center gap-3 text-xs text-muted-foreground">
                    <span>Created {new Intl.DateTimeFormat(undefined, { dateStyle: "short" }).format(new Date(key.createdAt))}</span>
                    {key.lastUsedAt ? (
                      <span>Last used {new Intl.DateTimeFormat(undefined, { dateStyle: "short" }).format(new Date(key.lastUsedAt))}</span>
                    ) : (
                      <span>Never used</span>
                    )}
                  </div>
                </div>

                {key.isActive ? (
                  <button
                    type="button"
                    onClick={() => revokeMut.mutate(key.id)}
                    disabled={revokeMut.isPending}
                    className={cn(buttonOutlineCompact, "text-destructive hover:bg-destructive/10 sm:w-fit")}
                  >
                    <Trash2 size={14} />
                    Revoke
                  </button>
                ) : null}
              </div>
            ))}
          </div>
        ) : (
          <div className="rounded-md border border-dashed border-foreground/20 p-6 text-center text-sm text-muted-foreground">
            <Terminal className="mx-auto mb-2 text-muted-foreground" size={24} />
            No API keys created yet. Generate one above to use with your AI coding assistant.
          </div>
        )}
      </div>
    </section>
  )
}
