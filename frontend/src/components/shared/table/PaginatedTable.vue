<script setup lang="ts">
  import { computed, onMounted, ref } from 'vue'
  import type { PageRequest, PageResult, TableHeader } from '@/types'

  const { headers, queryFunction } = defineProps<{
    headers: TableHeader[]
    queryFunction: Function
    hasActions?: boolean
  }>()

  defineExpose({ getFirstPage, getPage })

  const tableRows = ref<any[]>([])

  const sortBy = ref<string>()
  const descending = ref<boolean>(true)
  const hasNext = ref<boolean>(false)

  let pages: (string | null)[] = [null]
  const currentPage = ref<number>(0)
  const showPage = computed(() => currentPage.value + 1)
  const totalPages = ref<number>(1)

  const loading = ref(false)

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

  async function withLoading(action: () => Promise<void>): Promise<void> {
    if (loading.value) return
    loading.value = true
    try {
      await action()
    } catch (err) {
      // TO-DO Handle error
    } finally {
      loading.value = false
    }
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

  async function getFirstPage(): Promise<void> {
    await loadPage(0, [null])
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
    <div class="max-h-full grow overflow-auto">
      <table class="max-h-full w-full table-fixed text-center">
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
            <td v-for="header in headers" class="py-4">
              {{ row[header.key].display }}
            </td>
            <td v-if="hasActions">
              <slot name="actions" :row="row"></slot>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <div class="mt-4 flex w-full justify-end gap-4">
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
