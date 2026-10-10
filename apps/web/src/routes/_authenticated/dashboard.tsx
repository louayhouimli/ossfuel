import { useNavigate } from "@tanstack/react-router"
import { createFileRoute } from "@tanstack/react-router"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { Button } from "@workspace/ui/components/button"
import { apiFetch } from "@/lib/api"
import { authQueryOptions } from "@/lib/auth"

export const Route = createFileRoute("/_authenticated/dashboard")({
  component: Dashboard,
})

function Dashboard() {
  const { data: user } = useQuery(authQueryOptions())
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const handleLogout = async () => {
    await apiFetch("/logout", { method: "POST" })
    await queryClient.invalidateQueries({ queryKey: ["auth"] })
    await navigate({ to: "/login" })
  }

  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-4 p-6">
      <h1 className="text-2xl font-semibold">Welcome, {user?.login}</h1>
      <p className="text-muted-foreground">
        Signed in as {user?.email ?? user?.name ?? user?.login}
      </p>
      <Button onClick={handleLogout}>Sign out</Button>
    </div>
  )
}