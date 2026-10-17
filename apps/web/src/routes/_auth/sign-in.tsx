import { AuthShell } from "@/components/auth/auth-shell"
import { createFileRoute } from "@tanstack/react-router"

export const Route = createFileRoute("/_auth/sign-in")({
  head: () => ({
    meta: [
      { title: "Sign in | OSSFuel" },
      {
        name: "description",
        content:
          "Sign in with GitHub to register and manage your open-source projects on OSSFuel.",
      },
    ],
  }),
  component: AuthShell,
})
