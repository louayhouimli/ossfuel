import { useQuery } from "@tanstack/react-query"
import { Cpu, Zap } from "lucide-react"
import { Loader } from "@/components/loader"
import { aiUsageQueryOptions } from "@/lib/gateway"
import { eyebrowItem } from "@/lib/styles"

export function AIUsageAudit({ projectId }: { projectId: string }) {
  const usageQuery = useQuery(aiUsageQueryOptions(projectId))

  return (
    <section className="mt-8 border-t border-foreground/15 pt-8">
      <div>
        <span className={eyebrowItem}>Transparency Ledger</span>
        <h2 className="text-xl font-bold tracking-[-0.03em]">AI Usage Audit Log</h2>
        <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
          Every token spent through the gateway is audited in real time with prompt and completion counts and minor-unit USD cost.
        </p>
      </div>

      <div className="mt-4">
        {usageQuery.isLoading ? (
          <Loader label="Loading AI usage..." />
        ) : usageQuery.data && usageQuery.data.length > 0 ? (
          <div className="overflow-x-auto rounded-md border border-foreground/15">
            <table className="w-full text-left text-xs">
              <thead className="bg-muted/50 border-b border-foreground/10 text-muted-foreground">
                <tr>
                  <th className="p-2.5 font-medium">Model</th>
                  <th className="p-2.5 font-medium">Provider</th>
                  <th className="p-2.5 font-medium text-right">Prompt Tokens</th>
                  <th className="p-2.5 font-medium text-right">Completion</th>
                  <th className="p-2.5 font-medium text-right">Cost (USD)</th>
                  <th className="p-2.5 font-medium text-right">Timestamp</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-foreground/10 font-mono">
                {usageQuery.data.map((item) => (
                  <tr key={item.id} className="hover:bg-foreground/[0.02]">
                    <td className="p-2.5 font-semibold font-sans flex items-center gap-1.5">
                      <Cpu size={13} className="text-muted-foreground" />
                      {item.model}
                    </td>
                    <td className="p-2.5 text-muted-foreground">{item.provider}</td>
                    <td className="p-2.5 text-right">{item.promptTokens.toLocaleString()}</td>
                    <td className="p-2.5 text-right">{item.completionTokens.toLocaleString()}</td>
                    <td className="p-2.5 text-right text-emerald-500 font-semibold">
                      ${(item.costMinor / 100).toFixed(2)}
                    </td>
                    <td className="p-2.5 text-right text-muted-foreground font-sans">
                      {new Intl.DateTimeFormat(undefined, {
                        dateStyle: "short",
                        timeStyle: "short",
                      }).format(new Date(item.createdAt))}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="rounded-md border border-dashed border-foreground/20 p-6 text-center text-sm text-muted-foreground">
            <Zap className="mx-auto mb-2 text-muted-foreground" size={24} />
            No AI requests logged yet. Requests made using project API keys will appear here.
          </div>
        )}
      </div>
    </section>
  )
}
