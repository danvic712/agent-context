import axios from 'axios'

// All API calls go through axios (single instance, shared interceptors).
const http = axios.create({ baseURL: '/api' })

http.interceptors.response.use(
  (response) => response,
  (error) => {
    const message =
      error.response?.data?.message ??
      `Request failed with status ${error.response?.status ?? error.code ?? 'unknown'}`
    return Promise.reject(new Error(message))
  },
)

export interface SetupStatus {
  configured: boolean
}

export interface SetupResult {
  userId: string
  workspaceId: string
  workspaceName: string
}

export interface HealthStatus {
  status: string
  database: string
}

export type KnowledgeType = 'Problem' | 'Solution' | 'Pattern'

export interface KnowledgeItem {
  id: string
  type: KnowledgeType
  title: string
  content: string
  confidence: number
  isPrivate: boolean
  domainName: string | null
  sourceSessionTask: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface ReviewKnowledgeResult {
  threshold: number
  items: KnowledgeItem[]
}

export async function getSetupStatus(): Promise<SetupStatus> {
  const { data } = await http.get<SetupStatus>('/setup')
  return data
}

export async function postSetup(
  displayName: string,
  email: string,
  password: string,
): Promise<SetupResult> {
  const { data } = await http.post<SetupResult>('/setup', { displayName, email, password })
  return data
}

export async function getHealth(): Promise<HealthStatus> {
  const { data } = await http.get<HealthStatus>('/health')
  return data
}

export async function listKnowledge(): Promise<KnowledgeItem[]> {
  const { data } = await http.get<KnowledgeItem[]>('/knowledge')
  return data
}

export async function listReviewKnowledge(): Promise<ReviewKnowledgeResult> {
  const { data } = await http.get<ReviewKnowledgeResult>('/knowledge/review')
  return data
}

export async function setKnowledgePrivate(id: string, isPrivate: boolean): Promise<void> {
  await http.patch(`/knowledge/${id}`, { isPrivate })
}

export async function deleteKnowledge(id: string): Promise<void> {
  await http.delete(`/knowledge/${id}`)
}

