import { Outlet, createFileRoute, redirect } from "@tanstack/react-router"
import { Loader } from "@/components/loader"
import { authQueryOptions } from "@/lib/auth"

export const Route = createFileRoute("/_site/_authenticated")({
  beforeLoad: async ({ context }) => {
    const user = await context.queryClient.fetchQuery(authQueryOptions())

    if (!user) {
      throw redirect({ to: "/sign-in" })
    }
  },
  pendingMs: 200,
  pendingComponent: PendingComponent,
  component: RouteComponent,
})

function PendingComponent() {
  return (
    <main className="flex min-h-[60svh] items-center justify-center px-5">
      <Loader label="Checking your session" />
    </main>
  )
}

function RouteComponent() {
  return <Outlet />
}