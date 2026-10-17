import { useState } from "react"
import { Link, useRouterState } from "@tanstack/react-router"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { Menu, Moon, Sun, X } from "lucide-react"
import { Logo } from "@/components/brand/logo"
import { Loader } from "@/components/loader"
import { useTheme } from "@/components/theme-provider"
import { apiFetch } from "@/lib/api"
import { authQueryOptions } from "@/lib/auth"

const navLinkClass =
  "rounded-sm px-1 py-3 text-sm font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"

const sectionLinks = [
  ["Why OSSFuel", "#why"],
  ["The flow", "#flow"],
  ["The record", "#record"],
] as const

/**
 * The one navigation bar for the whole site.
 *
 * Renders from the `_site` layout route rather than per page, so it mounts once
 * and stays put across navigation. Sticky instead of fixed: it keeps its space
 * in the flow, so no page needs matching top padding.
 *
 * The landing page's section anchors only exist on the landing page. Showing
 * them elsewhere produced links that pointed at nothing.
 */
export function SiteHeader() {
  const [menuOpen, setMenuOpen] = useState(false)
  const [isSigningOut, setIsSigningOut] = useState(false)
  const { theme, setTheme } = useTheme()
  const { data: user, isLoading: isAuthLoading } = useQuery(authQueryOptions())
  const queryClient = useQueryClient()
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  })
  const onHomePage = pathname === "/"

  const toggleTheme = () => setTheme(theme === "dark" ? "light" : "dark")

  const handleSignOut = async () => {
    setIsSigningOut(true)

    try {
      await apiFetch("/logout", { method: "POST" })
      queryClient.setQueryData(["auth", "user"], null)
      await queryClient.invalidateQueries({ queryKey: ["auth", "user"] })
      setMenuOpen(false)
    } finally {
      setIsSigningOut(false)
    }
  }

  const themeButton = (
    <button
      type="button"
      onClick={toggleTheme}
      className="inline-flex min-h-11 items-center gap-2 rounded-sm px-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      aria-label={`Switch to ${theme === "dark" ? "light" : "dark"} theme`}
    >
      {theme === "dark" ? (
        <Sun size={16} aria-hidden="true" />
      ) : (
        <Moon size={16} aria-hidden="true" />
      )}
    </button>
  )

  const authArea = user ? (
    <div className="flex items-center gap-2">
      <Link
        to="/dashboard"
        className="inline-flex min-h-11 items-center gap-2 rounded-sm px-2 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        {user.avatarUrl ? (
          <img
            src={user.avatarUrl}
            alt={`${user.login} GitHub avatar`}
            className="h-8 w-8 rounded-full border border-foreground/20"
          />
        ) : (
          <span className="inline-flex h-8 w-8 items-center justify-center rounded-full border border-foreground/20 text-xs font-bold">
            {user.login.slice(0, 1).toUpperCase()}
          </span>
        )}
        <span className="hidden max-w-28 truncate sm:inline">{user.login}</span>
      </Link>
      <button
        type="button"
        onClick={handleSignOut}
        disabled={isSigningOut}
        className="min-h-11 rounded-sm px-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        {isSigningOut ? "Signing out" : "Sign out"}
      </button>
    </div>
  ) : isAuthLoading ? (
    <Loader label="Checking session" size={16} className="min-h-11 px-3" />
  ) : (
    <Link
      to="/sign-in"
      className="min-h-11 rounded-sm bg-primary px-4 py-3 text-sm font-semibold text-primary-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
    >
      Sign in with GitHub
    </Link>
  )

  return (
    <header className="sticky top-0 z-40 border-b border-foreground/15 bg-background">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-5 py-5 sm:px-8">
        <Link
          to="/"
          aria-label="OSSFuel home"
          className="rounded-sm focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
        >
          <Logo size={30} />
        </Link>

        <nav
          className="hidden items-center gap-8 md:flex"
          aria-label="Primary navigation"
        >
          {onHomePage
            ? sectionLinks.map(([label, href]) => (
                <a key={href} href={href} className={navLinkClass}>
                  {label}
                </a>
              ))
            : null}
          <Link to="/projects" className={navLinkClass}>
            Projects
          </Link>
          {user ? (
            <Link to="/dashboard" className={navLinkClass}>
              Dashboard
            </Link>
          ) : null}
        </nav>

        <div className="hidden items-center gap-2 md:flex">
          {themeButton}
          {authArea}
        </div>

        <button
          type="button"
          onClick={() => setMenuOpen((open) => !open)}
          aria-expanded={menuOpen}
          aria-controls="site-mobile-menu"
          aria-label="Toggle navigation"
          className="inline-flex min-h-11 items-center gap-2 rounded-sm border border-foreground/20 px-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring md:hidden"
        >
          {menuOpen ? (
            <X size={16} aria-hidden="true" />
          ) : (
            <Menu size={16} aria-hidden="true" />
          )}
          Menu
        </button>
      </div>

      {menuOpen ? (
        <nav
          id="site-mobile-menu"
          aria-label="Mobile navigation"
          className="border-t border-foreground/15 px-5 py-4 md:hidden"
        >
          <div className="mx-auto flex max-w-6xl flex-col gap-1">
            {onHomePage
              ? sectionLinks.map(([label, href]) => (
                  <a
                    key={href}
                    href={href}
                    onClick={() => setMenuOpen(false)}
                    className={navLinkClass}
                  >
                    {label}
                  </a>
                ))
              : null}
            <Link
              to="/projects"
              onClick={() => setMenuOpen(false)}
              className={navLinkClass}
            >
              Projects
            </Link>
            {user ? (
              <Link
                to="/dashboard"
                onClick={() => setMenuOpen(false)}
                className={navLinkClass}
              >
                Dashboard
              </Link>
            ) : null}
            <div className="mt-2">{themeButton}</div>
            <div className="mt-2">{authArea}</div>
          </div>
        </nav>
      ) : null}
    </header>
  )
}
