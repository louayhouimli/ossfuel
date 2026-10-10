import * as React from "react"
import { Outlet, createRootRouteWithContext } from "@tanstack/react-router"
import { TanStackRouterDevtoolsPanel } from "@tanstack/react-router-devtools"
import { QueryClient } from "@tanstack/react-query"
import { TanStackDevtools } from "@tanstack/react-devtools"
import { ReactQueryDevtoolsPanel } from "@tanstack/react-query-devtools"

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()(
  {
    // Typically we don't need the user immediately in landing pages.
    // For protected routes, see /_authenticated/route.tsx
    head: () => ({
      meta: [
        {
          charSet: "utf-8",
        },
        {
          name: "viewport",
          content: "width=device-width, initial-scale=1",
        },
        {
          title: "OSSFuel | Fund the open source you depend on",
        },
        {
          name: "description",
          content:
            "A funding, gateway, and accounting layer for open-source projects.",
        },
      ],
      links: [
        {
          rel: "icon",
          type: "image/svg+xml",
          href: "/favicon.svg",
        },
      ],
    }),
    shellComponent: RootComponent,
  }
)

function RootComponent() {
  return (
    <React.Fragment>
      <Outlet />
      <TanStackDevtools
        plugins={[
          {
            name: "TanStack Query",
            render: <ReactQueryDevtoolsPanel />,
          },
          {
            name: "TanStack Router",
            render: <TanStackRouterDevtoolsPanel />,
          },
        ]}
      />
    </React.Fragment>
  )
}
