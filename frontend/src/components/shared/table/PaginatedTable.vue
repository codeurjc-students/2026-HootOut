<script setup lang="ts">
  import { computed, onMounted, ref } from 'vue'
  import type { PageRequest, PageResult, TableHeader } from '@/types'

  const { headers, queryFunction } = defineProps<{
    headers: TableHeader[]
    queryFunction: Function
    hasActions?: boolean
  }>()

  defineExpose({ getFirstPage, getPage, refresh })

  const tableRows = ref<any[]>([])

  const sortBy = ref<string>()
  const descending = ref<boolean>(true)
  const hasNext = ref<boolean>(false)

  let pages: (string | null)[] = [null]
  const currentPage = ref<number>(0)
  const showPage = computed(() => currentPage.value + 1)
  const totalPages = ref<number>(1)

  const loading = ref(false)

  // Used by the "Sort by" control shown on mobile, where the header row is hidden
  const sortableHeaders = computed(() => headers.filter((h) => h.canSort))

  async function onSortSelect(event: Event) {
    await sortTable((event.target as HTMLSelectElement).value)
  }

  function buildRows(items: any[]): Object[] {
    let rows: Object[] = []
    for (let item of items) {
      const row: any = { ...item }
      for (let header of headers) {
        const value = row[header.key as keyof Object]
        const display = header.mapper ? (header.mapper(value) ?? value) : value
        row[header.key as keyof Object] = { value, display }
      }
      rows.push(row)
    }
    return rows
  }

  async function loadPage(targetPage: number, cursors: (string | null)[]): Promise<void> {
    const nextPageRequest: PageRequest = {
      next: cursors[targetPage] ?? null,
      descending: descending.value,
      pageSize: 10,
      sortBy: sortBy.value,
    }

    const page: PageResult<unknown> = await queryFunction(nextPageRequest)
    if (!page) return

    // Solo se actualiza el estado cuando la petición ha ido bien
    pages = cursors
    currentPage.value = targetPage
    tableRows.value = buildRows(page.items)
    hasNext.value = !!page.next
    totalPages.value = Math.max(1, Math.ceil(page.total / page.pageSize))

    if (hasNext.value) {
      pages[targetPage + 1] = page.next ?? null
    }
  }

  async function getPage(dir: number): Promise<void> {
    await loadPage(currentPage.value + dir, pages)
  }

  async function getFirstPage(): Promise<void> {
    await loadPage(0, [null])
  }

  async function withLoading(action: () => Promise<void>): Promise<void> {
    loading.value = true
    try {
      await action()
    } catch (err) {
      // TO-DO Handle error
    } finally {
      loading.value = false
    }
  }

  // Reloads the current page (e.g. after a row was changed from outside), with the same
  // loading state and error handling as the pagination buttons
  async function refresh() {
    await withLoading(() => getPage(0))
  }

  async function getNextPage() {
    await withLoading(() => getPage(+1))
  }

  async function getBeforePage() {
    await withLoading(() => getPage(-1))
  }

  async function sortTable(column: string) {
    descending.value = column !== sortBy.value ? true : !descending.value
    sortBy.value = column
    await withLoading(getFirstPage)
  }

  onMounted(async () => {
    sortBy.value = (headers.filter((h) => h.default).at(0) ?? headers[0])?.key?.toLowerCase()
    await withLoading(getFirstPage)
  })
</script>

<template>
  <div
    class="flex h-full max-h-full w-full flex-col justify-between overflow-x-auto overflow-y-hidden rounded-xl border-1 background-primary p-4"
  >
    <!-- Mobile only (hidden from 520px by CSS): the header row is hidden there -->
    <div
      v-if="sortableHeaders.length > 0"
      class="mobile-sort mb-4 flex items-center justify-end gap-2"
    >
      <label for="mobile-sort-by">Sort by</label>
      <select
        id="mobile-sort-by"
        class="inline-btn"
        :value="sortBy"
        :disabled="loading"
        @change="onSortSelect"
      >
        <option
          v-for="header in sortableHeaders"
          :key="header.key"
          :value="header.key.toLocaleLowerCase()"
        >
          {{ header.name }}
        </option>
      </select>
      <button
        type="button"
        class="mobile-sort-direction inline-btn pointer material-symbols-sharp w-8 text-center"
        :aria-label="descending ? 'Sorted descending' : 'Sorted ascending'"
        :disabled="loading"
        @click="sortTable(sortBy!)"
      >
        {{ descending ? 'keyboard_arrow_up' : 'keyboard_arrow_down' }}
      </button>
    </div>
    <div class="max-h-full grow overflow-auto">
      <table class="responsive-table max-h-full w-full table-fixed text-center">
        <thead class="sticky top-0 background-primary">
          <tr class="border-bottom-1">
            <th
              v-for="header in headers"
              class="py-2"
              :class="header.canSort ? 'pointer' : ''"
              @click="
                async () => {
                  if (header.canSort) await sortTable(header.key.toLocaleLowerCase())
                }
              "
            >
              <div class="align-items flex justify-center">
                <span class="inline-block">
                  {{ header.name }}
                </span>
                <span
                  class="material-symbols-sharp inline-block"
                  v-if="sortBy == header.key.toLocaleLowerCase() && header.canSort"
                >
                  {{ descending ? 'keyboard_arrow_up' : 'keyboard_arrow_down' }}
                </span>
              </div>
            </th>
            <th v-if="hasActions">Actions</th>
          </tr>
        </thead>
        <tbody class="overflow-y-auto">
          <tr v-for="row in tableRows" class="border-bottom-1 hover:background-light">
            <!-- data-label is shown next to the value in the mobile card layout -->
            <td v-for="header in headers" class="py-4" :data-label="header.name">
              {{ row[header.key].display }}
            </td>
            <td v-if="hasActions" class="actions-cell">
              <slot name="actions" :row="row"></slot>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <div class="gap-4c mt-4 flex w-full justify-end">
      <button
        v-if="showPage != 1"
        class="inline-btn pointer material-symbols-sharp w-8 text-center"
        @click="getBeforePage()"
        :disabled="loading"
      >
        chevron_left
      </button>
      <div v-else class="w-8"></div>
      <div>Page {{ showPage }} / {{ totalPages }}</div>
      <button
        v-if="hasNext"
        class="inline-btn pointer material-symbols-sharp w-8 text-center"
        @click="getNextPage()"
        :disabled="loading"
      >
        chevron_right
      </button>
      <div v-else class="w-8"></div>
    </div>
  </div>
</template>

<style scoped>
  /* Mobile: each row becomes a card with "Column: value" lines and the actions at the bottom */
  @media (max-width: 520px) {
    .responsive-table,
    .responsive-table tbody,
    .responsive-table tr,
    .responsive-table td {
      display: block;
      width: 100%;
    }

    .responsive-table thead {
      display: none;
    }

    .responsive-table tr {
      margin-bottom: 0.75rem;
      padding: 0.75rem;
      border: 1px solid color-mix(in srgb, currentColor 20%, transparent);
      border-radius: 0.75rem;
    }

    .responsive-table td {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      padding: 0.25rem 0;
      text-align: right;
      overflow-wrap: anywhere;
    }

    .responsive-table td::before {
      content: attr(data-label);
      flex-shrink: 0;
      font-weight: 600;
      text-align: left;
    }

    .responsive-table td.actions-cell {
      justify-content: flex-end;
      padding-top: 0.5rem;
    }

    .responsive-table td.actions-cell::before {
      content: none;
    }
  }

  @media (min-width: 520px) {
    .mobile-sort {
      display: none;
    }
  }
</style>
