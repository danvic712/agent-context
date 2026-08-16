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

export interface SkillItem {
  id: string
  domainName: string
  slug: string
  name: string
  description: string
  version: number
  createdAtUtc: string
  updatedAtUtc: string
}

export interface SkillDetail extends SkillItem {
  instructions: string
}

export interface SkillInput {
  domain: string
  slug: string
  name: string
  description: string
  instructions: string
}

export async function listSkills(): Promise<SkillItem[]> {
  const { data } = await http.get<SkillItem[]>('/skills')
  return data
}

export async function createSkill(input: SkillInput): Promise<SkillDetail> {
  const { data } = await http.post<SkillDetail>('/skills', input)
  return data
}

export async function getSkill(domain: string, slug: string): Promise<SkillDetail> {
  const { data } = await http.get<SkillDetail>('/skills/by-slug', { params: { domain, slug } })
  return data
}

export async function publishSkill(id: string, input: Omit<SkillInput, 'domain' | 'slug'>): Promise<SkillDetail> {
  const { data } = await http.post<SkillDetail>(`/skills/${id}/publish`, input)
  return data
}

export async function deleteSkill(id: string): Promise<void> {
  await http.delete(`/skills/${id}`)
}

