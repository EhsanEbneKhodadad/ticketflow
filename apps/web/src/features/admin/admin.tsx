"use client";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getApiV1AdminCategoriesOptions,
  getApiV1AdminCategoriesQueryKey,
  getApiV1AdminUsersOptions,
  getApiV1AdminUsersQueryKey,
  postApiV1AdminCategoriesMutation,
  putApiV1AdminCategoriesByIdMutation,
  patchApiV1AdminUsersByIdRoleMutation,
  type AdminCategoryView,
  type AdminUserView,
  type CategoryRequest,
  type ChangeRoleRequest,
} from "@ticketflow/api-client";
import { useAuth } from "@/features/auth/auth-provider";
import "@/lib/api";
import { useErrorMessage } from "@/lib/use-error-message";
import { Button } from "@/components/ui/button";

type Category = AdminCategoryView;
type AdminUser = AdminUserView;
export function Admin() {
  const t = useTranslations("Admin");
  const errorMessage = useErrorMessage();
  const { user } = useAuth();
  const client = useQueryClient();
  const principal = user?.id;
  const enabled = user?.role === "Admin";
  const headers = principal ? { "X-TicketFlow-User": principal } : undefined;
  const options = { headers };
  const categories = useQuery({
    ...getApiV1AdminCategoriesOptions(options),
    enabled,
    retry: false,
  });
  const users = useQuery({
    ...getApiV1AdminUsersOptions(options),
    enabled,
    retry: false,
  });
  if (!enabled)
    return (
      <section>
        <h1>{t("title")}</h1>
        <p role="alert">{t("access")}</p>
      </section>
    );
  return (
    <section>
      <div className="page-heading">
        <div>
          <p className="page-eyebrow">{t("eyebrow")}</p>
          <h1>{t("title")}</h1>
          <p>{t("description")}</p>
        </div>
      </div>
      <section className="card admin-section">
        <h2>{t("categories")}</h2>
        <CategoryForm
          key="new"
          options={options}
          onSaved={() =>
            void client.invalidateQueries({
              queryKey: getApiV1AdminCategoriesQueryKey(options),
            })
          }
        />
        {categories.isPending && <p role="status">{t("loadingCategories")}</p>}
        {categories.isError && (
          <div role="alert">
            {errorMessage(categories.error)}{" "}
            <Button onClick={() => void categories.refetch()}>
              {t("retry")}
            </Button>
          </div>
        )}
        {categories.data?.length === 0 && <p>{t("emptyCategories")}</p>}
        {categories.data?.map((category) => (
          <CategoryForm
            key={category.id}
            category={category}
            options={options}
            onSaved={() =>
              void client.invalidateQueries({
                queryKey: getApiV1AdminCategoriesQueryKey(options),
              })
            }
          />
        ))}
      </section>
      <section className="card admin-section">
        <h2>{t("users")}</h2>
        {users.isPending && <p role="status">{t("loadingUsers")}</p>}
        {users.isError && (
          <div role="alert">
            {errorMessage(users.error)}{" "}
            <Button onClick={() => void users.refetch()}>{t("retry")}</Button>
          </div>
        )}
        {users.data?.length === 0 && <p>{t("emptyUsers")}</p>}
        {users.data?.map((account) => (
          <UserRole
            key={account.id}
            account={account}
            currentId={principal!}
            options={options}
            onSaved={() =>
              void client.invalidateQueries({
                queryKey: getApiV1AdminUsersQueryKey(options),
              })
            }
          />
        ))}
      </section>
    </section>
  );
}
type Options = { headers?: Record<string, string> };
function CategoryForm({
  category,
  options,
  onSaved,
}: {
  category?: Category;
  options: Options;
  onSaved: () => void;
}) {
  const t = useTranslations("Admin");
  const errorMessage = useErrorMessage();
  const categorySchema = z.object({
    name: z.string().trim().min(1, t("nameRequired")).max(100),
    slug: z.string().trim().min(1, t("slugRequired")).max(100),
  });
  type CategoryFields = z.infer<typeof categorySchema>;
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CategoryFields>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: category?.name ?? "", slug: category?.slug ?? "" },
  });
  const [notice, setNotice] = useState("");
  const mutation = useMutation({
    mutationFn: (input: CategoryRequest) =>
      category
        ? putApiV1AdminCategoriesByIdMutation(options).mutationFn!(
            {
              path: { id: category.id },
              body: input,
              throwOnError: true,
            },
            undefined as never,
          )
        : postApiV1AdminCategoriesMutation(options).mutationFn!(
            {
              body: input,
              throwOnError: true,
            },
            undefined as never,
          ),
    onSuccess: (value) => {
      reset({
        name: category ? value.name : "",
        slug: category ? value.slug : "",
      });
      setNotice(category ? t("categorySaved") : t("categoryCreated"));
      onSaved();
    },
  });
  const prefix = category?.id ?? "new";
  return (
    <form
      className="admin-row"
      onSubmit={handleSubmit((input) => {
        setNotice("");
        mutation.mutate(input);
      })}
    >
      <div className="form-field">
        <label htmlFor={`name-${prefix}`}>
          {category ? `${t("nameFor")} ${category.name}` : t("newName")}
        </label>
        <input
          id={`name-${prefix}`}
          {...register("name")}
          aria-invalid={!!errors.name}
        />
        {errors.name && (
          <span className="field-error" role="alert">
            {errors.name.message}
          </span>
        )}
      </div>
      <div className="form-field">
        <label htmlFor={`slug-${prefix}`}>
          {category ? `${t("slugFor")} ${category.name}` : t("newSlug")}
        </label>
        <input
          id={`slug-${prefix}`}
          {...register("slug")}
          aria-invalid={!!errors.slug}
        />
        {errors.slug && (
          <span className="field-error" role="alert">
            {errors.slug.message}
          </span>
        )}
      </div>
      <Button disabled={mutation.isPending}>
        {category ? t("saveCategory") : t("createCategory")}
      </Button>
      {mutation.isError && (
        <p role="alert" className="field-error">
          {errorMessage(mutation.error)}
        </p>
      )}
      {notice && <p role="status">{notice}</p>}
    </form>
  );
}
function UserRole({
  account,
  currentId,
  options,
  onSaved,
}: {
  account: AdminUser;
  currentId: string;
  options: Options;
  onSaved: () => void;
}) {
  const t = useTranslations("Admin");
  const errorMessage = useErrorMessage();
  const [role, setRole] = useState(account.role);
  const [notice, setNotice] = useState("");
  const mutation = useMutation({
    mutationFn: (input: ChangeRoleRequest) =>
      patchApiV1AdminUsersByIdRoleMutation(options).mutationFn!(
        {
          path: { id: account.id },
          body: input,
          throwOnError: true,
        },
        undefined as never,
      ),
    onSuccess: () => {
      setNotice(t("roleUpdated"));
      onSaved();
    },
  });
  return (
    <div className="admin-user">
      <div>
        <strong>{account.name}</strong>
        <p>{account.email}</p>
      </div>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          setNotice("");
          mutation.mutate({ role });
        }}
      >
        <label htmlFor={`role-${account.id}`}>
          {t("roleFor")} {account.name}
        </label>
        <select
          id={`role-${account.id}`}
          value={role}
          onChange={(event) => setRole(event.target.value)}
          disabled={account.id === currentId}
        >
          <option value="Customer">{t("customer")}</option>
          <option value="Agent">{t("agent")}</option>
          <option value="Admin">{t("admin")}</option>
        </select>
        <Button
          disabled={
            account.id === currentId ||
            role === account.role ||
            mutation.isPending
          }
        >
          {t("saveRole")}
        </Button>
        {account.id === currentId && <small>{t("selfRole")}</small>}
        {mutation.isError && (
          <span role="alert" className="field-error">
            {errorMessage(mutation.error)}
          </span>
        )}
        {notice && <span role="status">{notice}</span>}
      </form>
    </div>
  );
}
