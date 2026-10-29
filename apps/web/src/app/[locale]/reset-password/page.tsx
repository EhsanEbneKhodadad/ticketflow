import { AuthScreen } from "@/features/auth/auth-screen";
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  return (
    <AuthScreen
      mode="reset"
      email={typeof params.email === "string" ? params.email : ""}
      token={typeof params.token === "string" ? params.token : ""}
    />
  );
}
