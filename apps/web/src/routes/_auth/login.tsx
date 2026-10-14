import { AuthShell } from "@/components/auth/auth-shell"
import { createFileRoute } from "@tanstack/react-router"

export const Route = createFileRoute("/_auth/login")({
  component: AuthShell,
})
