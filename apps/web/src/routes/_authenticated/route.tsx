import { Outlet, createFileRoute, redirect } from "@tanstack/react-router"
import { authQueryOptions } from "@/lib/auth"

export const Route = createFileRoute("/_authenticated")({
  beforeLoad: async ({ context }) => {
    const user = await context.queryClient.fetchQuery(authQueryOptions())

    if (!user) {
      throw redirect({ to: "/login" })
    }
  },
  component: RouteComponent,
})

function RouteComponent() {
  return <Outlet />
}