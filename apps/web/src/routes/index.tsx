import { useQuery } from "@tanstack/react-query"
import { createFileRoute } from "@tanstack/react-router"
import { Button } from "@workspace/ui/components/button"

export const Route = createFileRoute("/")({
  component: RouteComponent,
})

function RouteComponent() {
  const { data, isLoading, error } = useQuery({
    queryKey: ["health"],
    queryFn: async (): Promise<string> => {
      return fetch(`${import.meta.env.VITE_API_URL}/health`).then((res) => {
        if (!res.ok) throw new Error("failed to fetch health")
        return res.text()
      })
    },
  })
  return (
    <div className="flex min-h-svh p-6">
      <div className="flex max-w-md min-w-0 flex-col gap-4 text-sm leading-loose">
        <div>
          <h1 className="font-medium">Project ready!</h1>
          <p>You may now add components and start building.</p>
          <p>We&apos;ve already added the button component for you.</p>
          <Button className="mt-2">Button</Button>
        </div>
        <div className="font-mono text-xs text-muted-foreground">
          (Press <kbd>d</kbd> to toggle dark mode)
        </div>
        <div>
          {isLoading ? <p>Loading</p> : data}
          {error && error.message}
        </div>
      </div>
    </div>
  )
}
