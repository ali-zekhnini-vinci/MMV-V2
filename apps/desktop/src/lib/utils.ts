// Utilitaire shadcn/ui : fusion de classes conditionnelles et résolution des conflits Tailwind.
import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
