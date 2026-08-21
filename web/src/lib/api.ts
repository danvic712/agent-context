import axios from 'axios'

export class ApiError extends Error {
  readonly status: number | undefined
  readonly details: Record<string, unknown>

  constructor(
    message: string,
    status: number | undefined,
    details: Record<string, unknown> = {},
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.details = details
  }
}

// All API calls go through axios (single instance, shared interceptors).
const http = axios.create({ baseURL: '/api' })

http.interceptors.response.use(
  (response) => response,
  (error) => {
    const message =
      error.response?.data?.message ??
      `Request failed with status ${error.response?.status ?? error.code ?? 'unknown'}`
    return Promise.reject(new ApiError(message, error.response?.status, error.response?.data ?? {}))
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

export type InferenceCapability = 'Chat' | 'Embedding'

export interface InferenceProvider {
  id: string
  name: string
  providerType: string
  baseUrl: string
  apiKeyConfigured: boolean
  maskedApiKey: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface InferenceRoute {
  id: string
  capability: InferenceCapability
  providerId: string
  model: string
}

export interface InferenceConfiguration {
  configured: boolean
  id: string | null
  name: string | null
  providers: InferenceProvider[]
  routes: InferenceRoute[]
  updatedAtUtc: string | null
}

export interface InferenceProviderInput {
  id: string
  name: string
  providerType: string
  baseUrl: string
  apiKey: string
}

export interface InferenceRouteInput {
  id: string
  capability: InferenceCapability
  providerId: string
  model: string
}

export interface InferenceConfigurationInput {
  name: string
  providers: InferenceProviderInput[]
  routes: InferenceRouteInput[]
}

export interface InferenceValidationCheck {
  capability: InferenceCapability
  valid: boolean
  message: string | null
}

export interface InferenceValidationResult {
  valid: boolean
  checks: InferenceValidationCheck[]
}

export async function getInferenceConfiguration(): Promise<InferenceConfiguration> {
  const { data } = await http.get<InferenceConfiguration>('/inference/configuration')
  return data
}

export async function verifyInferenceConfiguration(
  input: InferenceConfigurationInput,
): Promise<InferenceValidationResult> {
  const { data } = await http.post<InferenceValidationResult>('/inference/configuration/verify', input)
  return data
}

export async function saveInferenceConfiguration(
  input: InferenceConfigurationInput,
): Promise<InferenceConfiguration> {
  const { data } = await http.put<InferenceConfiguration>('/inference/configuration', input)
  return data
}

export interface HealthStatus {
  status: string
  database: string
}

export interface DashboardUrlDto {
  url: string | null
}

export async function getDashboardUrl(): Promise<DashboardUrlDto> {
  const { data } = await http.get<DashboardUrlDto>('/health/dashboard')
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
  language: string,
  inferenceConfiguration: InferenceConfigurationInput,
): Promise<SetupResult> {
  const { data } = await http.post<SetupResult>('/setup', {
    displayName,
    email,
    password,
    language,
    inferenceConfiguration,
  })
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
  previousVersionId?: string | null
  domainName: string
  slug: string
  name: string
  description: string
  version: number
  createdAtUtc: string
  updatedAtUtc: string
  sourceType: SkillSourceType | null
}

export type SkillSourceType = 'manual' | 'zip' | 'skills_sh' | 'local_copy'

export type SkillListSort = 'updated-desc' | 'updated-asc' | 'name-asc' | 'name-desc' | 'version-desc' | 'version-asc'

export interface SkillListFilters {
  search?: string
  domain?: string
  sourceType?: SkillSourceType
  sort?: SkillListSort
}

export interface SkillFileInfo {
  path: string
  size: number
  binary: boolean
}

export interface SkillDetail extends SkillItem {
  manifest: SkillFileInfo[]
  isLatest: boolean
  folders: string[]
}

export interface SkillFileChange {
  path: string
  contentBase64: string
}

export interface SkillPathRename {
  from: string
  to: string
}

export interface PublishSkillVersionInput {
  name: string
  description: string
  instructions: string
  files?: SkillFileChange[]
  folders?: string[]
  renames?: SkillPathRename[]
  deletedPaths?: string[]
}

export interface SkillVersionSummary {
  id: string
  previousVersionId: string | null
  version: number
  name: string
  description: string
  createdAtUtc: string
  updatedAtUtc: string
  sourceType: SkillSourceType | null
  isLatest: boolean
}

export interface SkillHistory {
  latestId: string
  versions: SkillVersionSummary[]
}

export interface SkillListPage {
  pageSize: number
  cursor: string | null
  items: SkillItem[]
  hasMore: boolean
  nextCursor: string | null
}

export interface SkillInput {
  domain: string
  slug: string
  name: string
  description: string
  instructions: string
}

export interface SkillUploadInput {
  domain: string
  slug: string
  name: string
  description: string
  archive: File
}

export async function listSkills(
  pageSize = 20,
  cursor?: string | null,
  signal?: AbortSignal,
  filters?: SkillListFilters,
): Promise<SkillListPage> {
  const { data } = await http.get<SkillListPage>('/skills', {
    params: {
      pageSize,
      cursor: cursor ?? undefined,
      search: filters?.search || undefined,
      domain: filters?.domain || undefined,
      sourceType: filters?.sourceType || undefined,
      sort: filters?.sort || undefined,
    },
    signal,
  })
  return data
}

export async function uploadSkill(
  input: SkillUploadInput,
  onProgress?: (progress: number) => void,
  signal?: AbortSignal,
): Promise<SkillDetail> {
  const form = new FormData()
  form.append('domain', input.domain)
  form.append('slug', input.slug)
  form.append('name', input.name)
  form.append('description', input.description)
  form.append('archive', input.archive, input.archive.name)

  const { data } = await http.post<SkillDetail>('/skills/upload', form, {
    signal,
    onUploadProgress: (event) => {
      if (event.total) onProgress?.(Math.round((event.loaded / event.total) * 100))
    },
  })
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

export async function getSkillById(id: string): Promise<SkillDetail> {
  const { data } = await http.get<SkillDetail>(`/skills/${id}`)
  return data
}

export async function getSkillHistory(id: string): Promise<SkillHistory> {
  const { data } = await http.get<SkillHistory>(`/skills/${id}/history`)
  return data
}

export async function publishSkill(id: string, input: Omit<SkillInput, 'domain' | 'slug'>): Promise<SkillDetail> {
  const { data } = await http.post<SkillDetail>(`/skills/${id}/publish`, input)
  return data
}

export async function publishSkillVersion(id: string, input: PublishSkillVersionInput): Promise<SkillDetail> {
  const { data } = await http.post<SkillDetail>(`/skills/${id}/versions`, input)
  return data
}

export async function readSkillFile(id: string, path: string): Promise<Blob> {
  const { data } = await http.get<Blob>(`/skills/${id}/file`, { params: { path }, responseType: 'blob' })
  return data
}

export async function writeSkillFile(id: string, path: string, content: string | Blob): Promise<SkillDetail> {
  // A Blob body keeps binary assets byte-exact (used by rename); a string is
  // plain text. axios sends Blob bodies with their own content type.
  const isBlob = content instanceof Blob
  const { data } = await http.put<SkillDetail>(`/skills/${id}/file`, content, {
    params: { path },
    headers: isBlob ? {} : { 'Content-Type': 'text/plain; charset=utf-8' },
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
