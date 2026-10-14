import { Button } from "@workspace/ui/components/button"
import { Link } from "@tanstack/react-router"
import { Logo } from "@/components/brand/logo"
import { API_URL } from "@/lib/api"

export function AuthShell() {
  const handleSignIn = () => {
    const returnUrl = encodeURIComponent("/dashboard")
    window.location.href = `${API_URL}/login?returnUrl=${returnUrl}`
  }

  return (
    <main className="min-h-svh bg-background text-foreground">
      <div className="mx-auto grid min-h-svh max-w-7xl lg:grid-cols-[0.9fr_1.1fr]">
        <aside className="hidden flex-col justify-between bg-primary p-10 text-primary-foreground lg:flex xl:p-14">
          <Link
            to="/"
            aria-label="OSSFuel home"
            className="w-fit rounded-sm focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
          >
            <Logo size={34} />
          </Link>
          <div className="max-w-md">
            <h1 className="mt-6 text-5xl leading-[0.94] font-bold tracking-[-0.06em] xl:text-7xl">
              Keep the AI spend attached to the work.
            </h1>
            <p className="mt-8 max-w-sm leading-7 text-primary-foreground/65">
              Sponsor funding, maintainer requests, and provider costs in one
              visible accounting layer.
            </p>
          </div>
          <p className="text-sm text-primary-foreground/45">
            Early development. Built for open-source maintainers.
          </p>
        </aside>

        <section className="flex min-h-svh flex-col px-5 py-6 sm:px-10 sm:py-8">
          <div className="flex items-center justify-between lg:justify-end">
            <Link to="/" aria-label="OSSFuel home" className="lg:hidden">
              <Logo size={30} />
            </Link>
            <Link
              to="/"
              className="text-sm font-semibold text-muted-foreground underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
            >
              Back to OSSFuel
            </Link>
          </div>

          <div className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center py-16">
            <h2 className="mt-5 text-4xl leading-[0.95] font-bold tracking-[-0.06em] sm:text-5xl">
              Welcome back.
            </h2>
            <p className="mt-5 max-w-sm leading-7 text-muted-foreground">
              Sign in with GitHub to continue to your OSSFuel workspace.
            </p>

            <div className="mt-10 border-t border-foreground/15 pt-8">
              <Button
                type="button"
                variant="outline"
                onClick={handleSignIn}
                className="h-12 w-full justify-center gap-3 rounded-sm text-sm font-semibold"
              >
                <img
                  src="https://images.shadcnspace.com/assets/svgs/icon-github.svg"
                  alt=""
                  className="h-5 w-5 dark:hidden"
                />
                <img
                  src="https://images.shadcnspace.com/assets/svgs/icon-github-white.svg"
                  alt=""
                  className="hidden h-5 w-5 dark:block"
                />
                Continue with GitHub
              </Button>
            </div>

          </div>

          <p className="text-center text-xs text-muted-foreground lg:text-right">
            OSSFuel is in early development.
          </p>
        </section>
      </div>
    </main>
  )
}
