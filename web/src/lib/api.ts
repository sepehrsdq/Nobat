export class ApiError extends Error {
  status: number
  type: string | undefined

  constructor(status: number, type: string | undefined, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.type = type
  }
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)

  if (init.body !== undefined && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(path, { credentials: 'same-origin', ...init, headers })

  if (response.status === 204) {
    return undefined as T
  }

  if (!response.ok) {
    let title = 'خطای غیرمنتظره رخ داد.'
    let type: string | undefined

    try {
      const problem = (await response.json()) as { title?: string; type?: string }
      title = problem.title ?? title
      type = problem.type
    } catch {
      // Error body was not JSON; keep the generic message.
    }

    throw new ApiError(response.status, type, title)
  }

  return (await response.json()) as T
}
