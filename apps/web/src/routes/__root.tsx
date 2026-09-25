import * as React from "react"
import { Outlet, createRootRouteWithContext } from "@tanstack/react-router"
import { TanStackRouterDevtoolsPanel } from "@tanstack/react-router-devtools"
import { QueryClient } from "@tanstack/react-query"
import { TanStackDevtools } from "@tanstack/react-devtools"
import { ReactQueryDevtoolsPanel } from "@tanstack/react-query-devtools"

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()(
  {
    // Typically we don't need the user immediately in landing pages.
    // For protected routes, see /_auth/route.tsx
    // beforeLoad: ({ context }) => {
    //   void context.queryClient.query(authQueryOptions()).catch(noop);
    // },
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
          // scaffold:title
          title: "OSSFuel",
        },
        {
          name: "description",
          // scaffold:description
          content: "Finances made clear.",
        },
      ],
      links: [
        // Replace with your icons here, or remove if you have a favicon.ico in public/
        {
          rel: "icon",
          href: "https://mugnavo.com/favicon.ico",
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
