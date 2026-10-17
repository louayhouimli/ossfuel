import { createFileRoute } from "@tanstack/react-router"
import { LandingPage } from "@/components/landing-page"

export const Route = createFileRoute("/_site/")({
  head: () => ({
    meta: [
      { title: "OSSFuel | Fund the open source you depend on" },
      {
        name: "description",
        content:
          "Sponsors fund a project's AI Balance, maintainers spend it on AI tools, and every request becomes a visible usage record.",
      },
    ],
  }),
  component: LandingPage,
})
