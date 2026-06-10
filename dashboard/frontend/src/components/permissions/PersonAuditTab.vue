<template>
  <div>
    <!-- Loading people list -->
    <div v-if="store.loadingPeople" class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-300 mb-6">
      <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
      Loading people from all organizations…
    </div>

    <!-- Person error -->
    <UAlert v-if="personError" color="error" icon="i-heroicons-exclamation-circle" :description="personError" class="mb-4" />

    <!-- Person dropdown -->
    <div v-if="store.peopleList" class="mb-6">
      <div class="relative max-w-lg">
        <UInput
          v-autofocus
          name="person-filter" v-model="personFilter"
          @focus="handleDropdownFocus"
          placeholder="Search for a person…"
          icon="i-heroicons-magnifying-glass"
          size="sm"
          class="w-full app-search"
        />
        <!-- Dropdown list -->
        <div v-if="dropdownOpen && filteredPeople.length > 0"
          class="absolute z-20 mt-1 w-full max-h-64 overflow-y-auto bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg">
          <button v-for="p in filteredPeople" :key="p.descriptor"
            @click="selectPerson(p)"
            class="w-full flex items-center justify-between px-4 py-2.5 text-sm text-left hover:bg-primary-50 dark:hover:bg-primary-800/60 transition-colors"
            :class="selectedPerson?.descriptor === p.descriptor ? 'bg-primary-100 dark:bg-primary-800/80 font-semibold border-l-3 border-primary-500' : ''">
            <div class="truncate">
              <span class="font-medium text-gray-900 dark:text-gray-100">{{ p.display_name }}</span>
              <span v-if="p.unique_name" class="ml-2 text-xs text-gray-500 dark:text-gray-300">{{ p.unique_name }}</span>
            </div>
            <span class="ml-2 px-2 py-0.5 text-xs rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 shrink-0">{{ p.organization }}</span>
          </button>
        </div>
        <div v-else-if="dropdownOpen && personFilter.length >= 1 && filteredPeople.length === 0"
          class="absolute z-20 mt-1 w-full bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg px-4 py-3 text-sm text-gray-400 dark:text-gray-300 italic">
          No matching people
        </div>
      </div>
      <p class="text-xs text-gray-500 dark:text-gray-300 mt-1">{{ displayPeopleList?.people?.length ?? 0 }} people loaded across all organizations</p>
    </div>

    <!-- Click-outside overlay to close dropdown -->
    <div v-if="dropdownOpen" class="fixed inset-0 z-10" @click="dropdownOpen = false"></div>

    <!-- Selected person's groups -->
    <div v-if="selectedPerson" class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
      <div class="px-5 py-4 border-b border-gray-200 dark:border-gray-700">
        <span class="font-semibold text-gray-900 dark:text-gray-100 text-lg">{{ displaySelectedPerson.display_name }}</span>
        <span v-if="displaySelectedPerson.unique_name" class="ml-2 text-sm text-gray-500 dark:text-gray-300">{{ displaySelectedPerson.unique_name }}</span>
        <span class="ml-2 px-2 py-0.5 text-xs rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300">{{ displaySelectedPerson.organization }}</span>
      </div>
      <div class="px-5 py-4">
        <!-- Loading groups -->
        <div v-if="store.personGroups[selectedPerson.descriptor]?.loading" class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-300">
          <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
          Loading groups…
        </div>
        <!-- Groups loaded -->
        <div v-else-if="store.personGroups[selectedPerson.descriptor]?.groups">
          <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-3">Permission Groups ({{ store.personGroups[selectedPerson.descriptor].groups.length }})</h4>
          <div v-if="store.personGroups[selectedPerson.descriptor].groups.length === 0" class="text-sm text-gray-400 dark:text-gray-300 italic">No group memberships found</div>
          <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            <div v-for="pg in groupedPersonGroups" :key="pg.project"
              class="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
              <div class="px-3 py-2 bg-primary-50 dark:bg-primary-900/20 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
                <span class="text-xs font-semibold text-gray-800 dark:text-gray-200 truncate">{{ pg.project }}</span>
                <span class="ml-2 px-1.5 py-0.5 text-xs rounded-full bg-primary-100 dark:bg-primary-900/40 text-primary-700 dark:text-primary-300 font-medium shrink-0">{{ pg.groups.length }}</span>
              </div>
              <ul class="px-3 py-2 space-y-1 bg-white dark:bg-gray-800">
                <li v-for="g in pg.groups" :key="g" class="text-sm text-gray-700 dark:text-gray-300 flex items-center gap-2">
                  <span class="w-1.5 h-1.5 rounded-full bg-primary-400 shrink-0"></span>
                  {{ g }}
                </li>
              </ul>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useMonitorStore } from '../../stores/monitor.js'
import { useDemoMode, anonName, anonEmail, anonOrg } from '../../composables/useDemoMode.js'
import { transformPeopleList, transformPersonGroups } from '../../composables/demoTransform.js'

const store = useMonitorStore()
const { isDemoMode } = useDemoMode()

const personFilter = ref('')
const dropdownOpen = ref(false)
const selectedPerson = ref(null)
const personError = ref('')

onMounted(async () => {
  try {
    await store.fetchPeople()
  } catch (e) {
    personError.value = e?.message || 'Failed to load people. Check PAT configuration and Graph scope.'
  }
})

const displayPeopleList = computed(() => {
  void isDemoMode.value
  return transformPeopleList(store.peopleList)
})

const filteredPeople = computed(() => {
  if (!displayPeopleList.value?.people) return []
  const q = personFilter.value.trim().toLowerCase()
  if (!q) return displayPeopleList.value.people.slice(0, 50)
  return displayPeopleList.value.people.filter(p =>
    p.display_name.toLowerCase().includes(q) ||
    (p.unique_name && p.unique_name.toLowerCase().includes(q))
  ).slice(0, 50)
})

const displaySelectedPerson = computed(() => {
  if (!selectedPerson.value) return null
  void isDemoMode.value
  if (!isDemoMode.value) return selectedPerson.value
  return {
    ...selectedPerson.value,
    display_name: anonName(selectedPerson.value.display_name),
    unique_name: anonEmail(selectedPerson.value.unique_name),
    organization: anonOrg(selectedPerson.value.organization),
  }
})

function handleDropdownFocus() {
  if (selectedPerson.value) {
    personFilter.value = ''
  }
  dropdownOpen.value = true
}

async function selectPerson(person) {
  selectedPerson.value = person
  personFilter.value = isDemoMode.value ? anonName(person.display_name) : person.display_name
  dropdownOpen.value = false
  try {
    await store.fetchPersonGroups(person.organization, person.descriptor)
  } catch (e) {
    // Error is handled in the template via store state
  }
}

const groupedPersonGroups = computed(() => {
  if (!selectedPerson.value) return []
  void isDemoMode.value
  const raw = store.personGroups[selectedPerson.value.descriptor]
  const data = transformPersonGroups(raw)
  if (!data?.groups) return []

  const map = {}
  for (const g of data.groups) {
    const name = g.display_name || ''
    const match = name.match(/^\[(.+?)\]\\(.+)$/)
    const project = match ? match[1] : 'Organization-level'
    const groupName = match ? match[2] : name
    if (!map[project]) map[project] = []
    map[project].push(groupName)
  }

  return Object.entries(map)
    .sort(([a], [b]) => {
      if (a === 'Organization-level') return 1
      if (b === 'Organization-level') return -1
      return a.localeCompare(b)
    })
    .map(([project, groups]) => ({ project, groups: groups.sort() }))
})
</script>
