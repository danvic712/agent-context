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

export interface LlmOptionsDto {
  configured: boolean
  baseUrl: string | null
  maskedApiKey: string | null
  model: string | null
  embeddingModel: string | null
}

export interface LlmOptionsInput {
  baseUrl: string
  apiKey: string
  model: string
  embeddingModel?: string | null
}

export async function getLlmOptions(): Promise<LlmOptionsDto> {
  const { data } = await http.get<LlmOptionsDto>('/settings/llm-options')
  return data
}

export async function saveLlmOptions(input: LlmOptionsInput): Promise<LlmOptionsDto> {
  const { data } = await http.put<LlmOptionsDto>('/settings/llm-options', input)
  return data
}

export interface LanguageDto {
  language: string
}

export async function getLanguage(): Promise<LanguageDto> {
  const { data } = await http.get<LanguageDto>('/settings/language')
  return data
}

export async function saveLanguage(language: string): Promise<LanguageDto> {
  const { data } = await http.put<LanguageDto>('/settings/language', { language })
  return data
}

export interface ThemeDto {
  theme: string
}

export async function getTheme(): Promise<ThemeDto> {
  const { data } = await http.get<ThemeDto>('/settings/theme')
  return data
}

export async function saveTheme(theme: string): Promise<ThemeDto> {
  const { data } = await http.put<ThemeDto>('/settings/theme', { theme })
  return data
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

export interface SkillFileInfo {
  path: string
  size: number
  binary: boolean
}

export interface SkillDetail extends SkillItem {
  manifest: SkillFileInfo[]
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

export async function readSkillFile(id: string, path: string): Promise<Blob> {
  const { data } = await http.get<Blob>(`/skills/${id}/file`, { params: { path }, responseType: 'blob' })
  return data
}

export async function writeSkillFile(id: string, path: string, content: string): Promise<SkillDetail> {
  const { data } = await http.put<SkillDetail>(`/skills/${id}/file`, content, {
    params: { path },
    headers: { 'Content-Type': 'text/plain; charset=utf-8' },
  })
  return data
}

export async function deleteSkillFile(id: string, path: string): Promise<SkillDetail> {
  const { data } = await http.delete<SkillDetail>(`/skills/${id}/file`, { params: { path } })
  return data
}

export async function uploadSkillFiles(id: string, files: File[]): Promise<SkillDetail> {
  const form = new FormData()
  for (const file of files) {
    form.append('files', file, file.name)
  }
  const { data } = await http.post<SkillDetail>(`/skills/${id}/files`, form)
  return data
}

export async function importSkillZip(id: string, zip: File): Promise<SkillDetail> {
  const form = new FormData()
  form.append('archive', zip, zip.name)
  const { data } = await http.post<SkillDetail>(`/skills/${id}/import`, form)
  return data
}

export async function deleteSkill(id: string): Promise<void> {
  await http.delete(`/skills/${id}`)
}

export interface AnalyticsGroupItem {
  name: string
  sessions: number
  tokensIn: number
  tokensOut: number
  cost: number
}

export interface AnalyticsOverview {
  totalSessions: number
  totalTokensIn: number
  totalTokensOut: number
  totalCost: number
  byDomain: AnalyticsGroupItem[]
  byAgent: AnalyticsGroupItem[]
}

export interface ModelPricing {
  id: string
  model: string
  inputCostPerToken: number
  outputCostPerToken: number
  updatedAtUtc: string
}

export interface OverviewFilters {
  domain?: string
  agent?: string
}

export async function getOverview(filters: OverviewFilters = {}): Promise<AnalyticsOverview> {
  const { data } = await http.get<AnalyticsOverview>('/analytics/overview', { params: filters })
  return data
}

export async function listPricing(): Promise<ModelPricing[]> {
  const { data } = await http.get<ModelPricing[]>('/analytics/pricing')
  return data
}

export async function savePricing(model: string, inputCostPerToken: number, outputCostPerToken: number): Promise<ModelPricing> {
  const { data } = await http.put<ModelPricing>('/analytics/pricing', {
    model,
    inputCostPerToken,
    outputCostPerToken,
  })
  return data
}

export async function deletePricing(model: string): Promise<void> {
  await http.delete(`/analytics/pricing/${encodeURIComponent(model)}`)
}

export interface EngineHealth {
  queuedSessions: number
  processingSessions: number
  failedSessions: number
  retryScheduledSessions: number
  totalSessions: number
}

export interface HygieneResult {
  decayed: number
  movedToReview: number
  archived: number
}

export async function getEngineHealth(): Promise<EngineHealth> {
  const { data } = await http.get<EngineHealth>('/health/engine')
  return data
}

export async function listArchivedKnowledge(): Promise<KnowledgeItem[]> {
  const { data } = await http.get<KnowledgeItem[]>('/knowledge/archived')
  return data
}

export async function restoreKnowledge(id: string): Promise<void> {
  await http.post(`/knowledge/${id}/restore`)
}

export async function runHygiene(): Promise<HygieneResult> {
  const { data } = await http.post<HygieneResult>('/knowledge/hygiene/run')
  return data
}

