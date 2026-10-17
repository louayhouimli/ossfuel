import * as React from "react"
import {
  HeadContent,
  Outlet,
  createRootRouteWithContext,
} from "@tanstack/react-router"
import { TanStackRouterDevtoolsPanel } from "@tanstack/react-router-devtools"
import { QueryClient } from "@tanstack/react-query"
import { TanStackDevtools } from "@tanstack/react-devtools"
import { ReactQueryDevtoolsPanel } from "@tanstack/react-query-devtools"

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()(
  {
    // Typically we don't need the user immediately in landing pages.
    // For protected routes, see /_site/_authenticated/route.tsx
    //
    // Only title and description live here. The static shell tags (charset,
    // viewport, favicon) stay in index.html, since every route would otherwise
    // re-declare them on navigation. Nested routes override both values.
    head: () => ({
      meta: [
        {
          title: "OSSFuel | Fund the open source you depend on",
        },
        {
          name: "description",
          content:
            "A funding, gateway, and accounting layer for open-source projects.",
        },
      ],
    }),
    shellComponent: RootComponent,
  }
)

function RootComponent() {
  return (
    <React.Fragment>
      <HeadContent />
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
