import { Button } from "@workspace/ui/components/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@workspace/ui/components/card"
import { Logo } from "@/components/brand/logo"

const RegisterForm = () => {
  return (
    <section className="relative flex min-h-screen items-center justify-center">
      <div className="mx-auto w-full max-w-lg px-4 py-10 sm:px-0 md:py-20">
        <Card className="relative max-w-lg gap-6 px-6 py-8 sm:p-12">
          <CardHeader className="gap-6 p-0 text-center">
            <Logo size={32} className="justify-center" />
            <div className="flex flex-col gap-1">
              <CardTitle className="text-2xl font-medium text-card-foreground">
                Welcome to OSSFuel
              </CardTitle>
              <CardDescription className="text-sm font-normal text-muted-foreground">
                Create your account with GitHub
              </CardDescription>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            <Button
              type="button"
              variant="outline"
              className="text-medium h-10 w-full cursor-pointer gap-2 rounded-lg text-sm text-card-foreground shadow-xs dark:bg-background"
            >
              <img
                src="https://images.shadcnspace.com/assets/svgs/icon-github.svg"
                alt="github icon"
                className="h-4 w-4 dark:hidden"
              />
              <img
                src="https://images.shadcnspace.com/assets/svgs/icon-github-white.svg"
                alt="github icon"
                className="hidden h-4 w-4 dark:block"
              />
              Continue with GitHub
            </Button>
          </CardContent>
        </Card>
      </div>
    </section>
  )
}

export default RegisterForm