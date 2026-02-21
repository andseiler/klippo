import type {
  ReviewDataResponse,
  UpdateEntityRequest,
  EntityDto,
  AddEntityRequest,
  SegmentDto,
  ReviewSummary,
  ConfidenceTier,
  ReviewStatus,
} from '../types'

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

let entityIdCounter = 200

const MOCK_SEGMENTS: SegmentDto[] = [
  {
    id: 'seg-1',
    segmentIndex: 0,
    textContent:
      'Arbeitsvertrag zwischen der Muster GmbH, vertreten durch Herrn Dr. Thomas Müller, Geschäftsführer, Berliner Straße 42, 10115 Berlin, und Frau Maria Schmidt, geboren am 15.03.1985, wohnhaft in der Hauptstraße 17, 80331 München.',
    sourceType: 'paragraph',
    sourceLocation: null,
  },
  {
    id: 'seg-2',
    segmentIndex: 1,
    textContent:
      'Die Arbeitnehmerin ist unter der E-Mail-Adresse maria.schmidt@example.de und telefonisch unter +49 89 123456789 erreichbar. Die Bankverbindung für Gehaltszahlungen lautet: IBAN DE89 3704 0044 0532 0130 00 bei der Commerzbank AG.',
    sourceType: 'paragraph',
    sourceLocation: null,
  },
  {
    id: 'seg-3',
    segmentIndex: 2,
    textContent:
      'Die Steuer-Identifikationsnummer der Arbeitnehmerin lautet 65 929 970 489. Das monatliche Bruttogehalt beträgt 5.200,00 EUR. Der Vertrag beginnt am 01.04.2025 und ist unbefristet. Ansprechpartner in der Personalabteilung ist Herr Klaus Weber, Durchwahl +49 30 9876543.',
    sourceType: 'paragraph',
    sourceLocation: null,
  },
  {
    id: 'seg-4',
    segmentIndex: 3,
    textContent:
      'Gerichtsstand ist Berlin. Beide Parteien bestätigen die Richtigkeit der vorstehenden Angaben. Unterschriften erfolgen digital über das Unternehmenssystem.',
    sourceType: 'paragraph',
    sourceLocation: null,
  },
]

const MOCK_ENTITIES: EntityDto[] = [
  {
    id: 'ent-1',
    segmentId: 'seg-1',
    text: 'Dr. Thomas Müller',
    entityType: 'PERSON',
    startOffset: 68,
    endOffset: 85,
    confidence: 0.97,
    detectionSources: ['ner', 'context'],
    confidenceTier: 'HIGH',
    replacementPreview: '[PERSON-1]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-2',
    segmentId: 'seg-1',
    text: 'Berliner Straße 42, 10115 Berlin',
    entityType: 'ADDRESS',
    startOffset: 104,
    endOffset: 136,
    confidence: 0.93,
    detectionSources: ['ner', 'regex'],
    confidenceTier: 'HIGH',
    replacementPreview: '[ADRESSE-1]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-3',
    segmentId: 'seg-1',
    text: 'Maria Schmidt',
    entityType: 'PERSON',
    startOffset: 147,
    endOffset: 160,
    confidence: 0.95,
    detectionSources: ['ner'],
    confidenceTier: 'HIGH',
    replacementPreview: '[PERSON-2]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-4',
    segmentId: 'seg-1',
    text: '15.03.1985',
    entityType: 'DATE_OF_BIRTH',
    startOffset: 175,
    endOffset: 185,
    confidence: 0.78,
    detectionSources: ['regex', 'context'],
    confidenceTier: 'MEDIUM',
    replacementPreview: '[GEBURTSDATUM-1]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-5',
    segmentId: 'seg-1',
    text: 'Hauptstraße 17, 80331 München',
    entityType: 'ADDRESS',
    startOffset: 201,
    endOffset: 230,
    confidence: 0.91,
    detectionSources: ['ner', 'regex'],
    confidenceTier: 'HIGH',
    replacementPreview: '[ADRESSE-2]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-6',
    segmentId: 'seg-2',
    text: 'maria.schmidt@example.de',
    entityType: 'EMAIL',
    startOffset: 50,
    endOffset: 74,
    confidence: 0.99,
    detectionSources: ['regex', 'checksum'],
    confidenceTier: 'HIGH',
    replacementPreview: '[EMAIL-1]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-7',
    segmentId: 'seg-2',
    text: '+49 89 123456789',
    entityType: 'PHONE_DACH',
    startOffset: 101,
    endOffset: 118,
    confidence: 0.88,
    detectionSources: ['regex'],
    confidenceTier: 'HIGH',
    replacementPreview: '[TELEFON-1]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-8',
    segmentId: 'seg-2',
    text: 'DE89 3704 0044 0532 0130 00',
    entityType: 'IBAN',
    startOffset: 171,
    endOffset: 198,
    confidence: 0.99,
    detectionSources: ['regex', 'checksum'],
    confidenceTier: 'HIGH',
    replacementPreview: '[IBAN-1]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-9',
    segmentId: 'seg-3',
    text: '65 929 970 489',
    entityType: 'DE_STEUER_ID',
    startOffset: 53,
    endOffset: 67,
    confidence: 0.96,
    detectionSources: ['regex', 'checksum'],
    confidenceTier: 'HIGH',
    replacementPreview: '[STEUER-ID-1]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-10',
    segmentId: 'seg-3',
    text: '5.200,00 EUR',
    entityType: 'FINANCIAL_AMOUNT',
    startOffset: 100,
    endOffset: 112,
    confidence: 0.55,
    detectionSources: ['ner'],
    confidenceTier: 'LOW',
    replacementPreview: '[BETRAG-1]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-11',
    segmentId: 'seg-3',
    text: '01.04.2025',
    entityType: 'DATE',
    startOffset: 135,
    endOffset: 145,
    confidence: 0.62,
    detectionSources: ['regex'],
    confidenceTier: 'MEDIUM',
    replacementPreview: '[DATUM-1]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-12',
    segmentId: 'seg-3',
    text: 'Klaus Weber',
    entityType: 'PERSON',
    startOffset: 208,
    endOffset: 219,
    confidence: 0.89,
    detectionSources: ['ner'],
    confidenceTier: 'HIGH',
    replacementPreview: '[PERSON-3]',
    reviewStatus: 'pending',
  },
  {
    id: 'ent-13',
    segmentId: 'seg-3',
    text: '+49 30 9876543',
    entityType: 'PHONE_DACH',
    startOffset: 232,
    endOffset: 246,
    confidence: 0.85,
    detectionSources: ['regex'],
    confidenceTier: 'HIGH',
    replacementPreview: '[TELEFON-2]',
    reviewStatus: 'pending',
  },
]

// Mutable state for dev testing
let mockEntities = MOCK_ENTITIES.map((e) => ({ ...e }))

function computeSummary(entities: EntityDto[]): ReviewSummary {
  return {
    totalEntities: entities.length,
    highConfidence: entities.filter((e) => e.confidenceTier === 'HIGH').length,
    mediumConfidence: entities.filter((e) => e.confidenceTier === 'MEDIUM').length,
    lowConfidence: entities.filter((e) => e.confidenceTier === 'LOW').length,
    confirmed: entities.filter((e) => e.reviewStatus === 'confirmed').length,
    manuallyAdded: entities.filter((e) => e.reviewStatus === 'addedmanual').length,
    pending: entities.filter((e) => e.reviewStatus === 'pending').length,
  }
}

export async function mockGetReviewData(jobId: string): Promise<ReviewDataResponse> {
  await delay(300 + Math.random() * 500)
  mockEntities = MOCK_ENTITIES.map((e) => ({ ...e }))
  return {
    jobId,
    status: 'inreview',
    segments: MOCK_SEGMENTS.map((s) => ({ ...s })),
    entities: mockEntities.map((e) => ({ ...e })),
    summary: computeSummary(mockEntities),
  }
}

export async function mockUpdateEntity(
  _jobId: string,
  entityId: string,
  request: UpdateEntityRequest,
): Promise<EntityDto> {
  await delay(200 + Math.random() * 300)
  const entity = mockEntities.find((e) => e.id === entityId)
  if (!entity) throw new Error('Entity not found')
  if (request.reviewStatus) entity.reviewStatus = request.reviewStatus as ReviewStatus
  if (request.entityType) entity.entityType = request.entityType
  if (request.startOffset !== undefined) entity.startOffset = request.startOffset
  if (request.endOffset !== undefined) entity.endOffset = request.endOffset
  return { ...entity }
}

export async function mockAddEntity(
  _jobId: string,
  request: AddEntityRequest,
): Promise<EntityDto> {
  await delay(300 + Math.random() * 300)
  entityIdCounter++
  const newEntity: EntityDto = {
    id: `ent-manual-${entityIdCounter}`,
    segmentId: request.segmentId,
    text: request.text,
    entityType: request.entityType,
    startOffset: request.startOffset,
    endOffset: request.endOffset,
    confidence: 1.0,
    detectionSources: ['manual'],
    confidenceTier: 'HIGH' as ConfidenceTier,
    replacementPreview: `[${request.entityType}-M]`,
    reviewStatus: 'addedmanual',
  }
  mockEntities.push(newEntity)
  return { ...newEntity }
}

