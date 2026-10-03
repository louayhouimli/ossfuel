import { createFileRoute } from "@tanstack/react-router"
import { Logo } from "@/components/brand/logo"

export const Route = createFileRoute("/")({
  component: RouteComponent,
})

function RouteComponent() {
  return (
    <div className="flex min-h-svh items-center justify-center p-6">
      <Logo size={96} showWordmark={false} />
    </div>
  )
}
