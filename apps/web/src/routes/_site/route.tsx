import { Outlet, createFileRoute } from "@tanstack/react-router"
import { SiteFooter } from "@/components/site-footer"
import { SiteHeader } from "@/components/site-header"

export const Route = createFileRoute("/_site")({
  component: SiteLayout,
})

/**
 * Shared chrome for every public and signed-in screen.
 *
 * A pathless layout, so it adds no URL segment. The sign-in route sits outside
 * it on purpose: that screen is a full-height split panel with its own logo.
 *
 * `overflow-x-clip` rather than `overflow-x-hidden`: the landing page hangs
 * decorative markers outside their column, and `hidden` would turn this element
 * into a scroll container, which breaks the sticky header inside it.
 */
function SiteLayout() {
  return (
    <div className="flex min-h-svh flex-col overflow-x-clip">
      <SiteHeader />
      <div className="flex-1">
        <Outlet />
      </div>
      <SiteFooter />
    </div>
  )
}
