import { Logo } from "@/components/brand/logo"

export function SiteFooter() {
  return (
    <footer className="border-t border-foreground/15">
      <div className="mx-auto flex max-w-6xl flex-col gap-4 px-5 py-8 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between sm:px-8">
        <Logo size={24} />
        <p>Open-source funding with the AI spend in view.</p>
      </div>
    </footer>
  )
}
