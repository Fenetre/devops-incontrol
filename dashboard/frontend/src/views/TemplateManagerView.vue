<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Template Manager</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          Create, delete, and copy work item templates across Azure DevOps projects.
        </p>
      </div>
    </div>

    <!-- PAT Warning -->
    <UAlert v-if="!store.patConfigured" color="warning" icon="i-heroicons-exclamation-triangle" description="PAT not configured. Set it in Settings first." class="mb-6" />

    <!-- Tabs -->
    <UTabs :items="tabItems" v-model="activeTab" :content="false" variant="link" class="mb-6" />

    <!-- ===================== CREATE TEMPLATE TAB ===================== -->
    <div v-if="activeTab === 'create'">

      <!-- Project & Team selector -->
      <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Project</label>
            <USelectMenu v-model="createProjectId" :items="projectOptions" value-key="value"
              placeholder="Choose a project…" class="w-full" />
          </div>
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Team</label>
            <USelectMenu v-model="createTeam" :items="createTeamOptions" value-key="value"
              :loading="loadingCreateTeams" :disabled="!createProjectId || loadingCreateTeams"
              placeholder="Choose a team…" class="w-full" />
          </div>
        </div>
      </div>

      <!-- Step 2: Work Item Type -->
      <div v-if="createStep >= 2" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
        <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-3">
          Work Item Type
        </h3>
        <USelectMenu v-model="createWit" :items="createWitOptions" value-key="value"
          :loading="loadingCreateWits" :disabled="loadingCreateWits"
          placeholder="Choose a work item type…" class="w-full max-w-md" />
      </div>

      <!-- Existing Templates -->
      <div v-if="createStep >= 2" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
        <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-3 flex items-center gap-2">
          Existing Templates
          <span v-if="deleteSuccess || createSuccess" class="ml-auto text-sm font-normal text-green-600 dark:text-green-400 flex items-center gap-1">
            <UIcon :name="deleteSuccess ? 'i-heroicons-trash' : 'i-heroicons-check-circle'" class="w-4 h-4" />
            {{ deleteSuccess || createSuccess }}
          </span>
        </h3>
        <p class="text-xs text-gray-500 dark:text-gray-400 mb-3">Use an existing template as a starting point, or manage templates for this team.</p>

        <div v-if="loadingCreateTemplates" class="flex items-center gap-2 text-sm text-gray-500">
          <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
          Loading templates…
        </div>
        <div v-else-if="createTemplates.length === 0" class="text-sm text-gray-500 dark:text-gray-400 italic">
          No templates found for this team.
        </div>
        <div v-else class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider bg-gray-50 dark:bg-gray-700/30">
                <th class="px-4 py-3">Name</th>
                <th class="px-4 py-3">Type</th>
                <th class="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="tmpl in createTemplates" :key="tmpl.id" class="border-b border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/40 transition-colors">
                <td class="px-4 py-3 text-gray-800 dark:text-gray-100">{{ isDemoMode ? anonPrTitle(tmpl.name) : tmpl.name }}</td>
                <td class="px-4 py-3">
                  <span class="text-xs bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 rounded-full px-2 py-0.5 font-medium">{{ tmpl.workItemTypeName }}</span>
                </td>
                <td class="px-4 py-3 text-right">
                  <div class="flex items-center justify-end gap-1">
                    <UButton variant="soft" color="primary" size="xs" icon="i-heroicons-document-duplicate" label="Use as base" :disabled="tmpl.id.startsWith('_pending_')" @click="useAsStartingPoint(tmpl)" />
                    <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-trash" :disabled="tmpl.id.startsWith('_pending_')" @click="confirmDeleteTemplate(tmpl)" />
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Template Details -->
      <div v-if="createStep >= 3" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
        <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-5">
          Template Details
        </h3>

        <div class="space-y-6">
          <!-- Name & Description -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Template Name *</label>
              <UInput name="create-name" v-model="createName" placeholder="e.g. Feature Template v2" class="w-full" />
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-1">A short, recognisable name for this template.</p>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Description</label>
              <UInput name="create-description" v-model="createDescription" placeholder="Optional description" class="w-full" />
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-1">Helps others understand when to use this template.</p>
            </div>
          </div>

          <!-- Fields editor -->
          <div>
            <div class="flex items-center justify-between mb-3">
              <div>
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">Default Field Values</label>
                <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">Set default values that will be pre-filled when creating work items from this template.</p>
              </div>
              <UButton variant="soft" color="primary" size="sm" icon="i-heroicons-plus" label="Add Field"
                :disabled="witFields.length === 0 || createFields.length >= witFields.length" @click="addField" />
            </div>

            <div v-if="loadingWitFields" class="flex items-center gap-2 text-sm text-gray-500 py-4">
              <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
              Loading available fields…
            </div>

            <div v-else-if="createFields.length === 0" class="border border-dashed border-gray-200 dark:border-gray-700 rounded-lg py-6 text-center text-sm text-gray-500 dark:text-gray-400">
              No fields added yet. Click "Add Field" to set default values.
            </div>

            <div v-else class="space-y-2">
              <div v-for="(field, i) in createFields" :key="i"
                class="flex items-center gap-3 bg-gray-50 dark:bg-gray-700/30 rounded-lg px-4 py-3">
                <div class="flex-1 min-w-0">
                  <USelectMenu v-model="field.key" :items="availableFieldsFor(i)" value-key="value"
                    placeholder="Select field…" class="w-full" />
                </div>
                <div class="flex-1 min-w-0">
                  <UInput name="value" v-model="field.value" placeholder="Default value" class="w-full" />
                </div>
                <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-x-mark" @click="createFields.splice(i, 1)" />
              </div>
            </div>
          </div>

          <!-- Create button -->
          <div v-if="canCreate" ref="createBtnRef" class="flex items-center gap-3 pt-5 border-t border-gray-200 dark:border-gray-700">
            <UButton
              @click="showCreateConfirm = true"
              :disabled="creating || created || isDemoMode"
              :loading="creating"
              :icon="created ? 'i-heroicons-check-circle' : 'i-heroicons-plus'"
              :label="creating ? 'Creating…' : created ? 'Created!' : 'Create Template'"
              :color="created ? 'success' : 'primary'"
            />
            <UButton v-if="created" @click="resetCreateForm" icon="i-heroicons-plus" variant="outline" color="neutral" label="Create another" />
          </div>
        </div>
      </div>

      <!-- Create error -->
      <UAlert v-if="createError" color="error" icon="i-heroicons-exclamation-triangle" :description="createError" class="mt-4" />

      <!-- Create Confirmation Modal -->
      <UModal :open="showCreateConfirm" @update:open="v => { if (!v) showCreateConfirm = false }" title="Create Template?" description="Confirm template creation">
        <template #body>
          <p class="text-sm text-gray-700 dark:text-gray-300">
            Create template "<strong>{{ createName }}</strong>" ({{ createWit }}) in the selected project and team?
          </p>
          <p v-if="isDemoMode" class="text-sm text-amber-600 dark:text-amber-400 mt-2">Disabled in demo mode.</p>
        </template>
        <template #footer>
          <div class="flex justify-end gap-2">
            <UButton variant="outline" color="neutral" label="Cancel" @click="showCreateConfirm = false" />
            <UButton label="Create" :disabled="isDemoMode" :loading="creating" @click="doCreateTemplate" />
          </div>
        </template>
      </UModal>

      <!-- Delete Confirmation Modal -->
      <UModal :open="showDeleteConfirm" @update:open="v => { if (!v) showDeleteConfirm = false }" title="Delete Template?" description="Confirm template deletion">
        <template #body>
          <p class="text-sm text-gray-700 dark:text-gray-300">
            Delete template "<strong>{{ deleteTarget?.name }}</strong>"? This cannot be undone.
          </p>
          <p v-if="isDemoMode" class="text-sm text-amber-600 dark:text-amber-400 mt-2">Disabled in demo mode.</p>
        </template>
        <template #footer>
          <div class="flex justify-end gap-2">
            <UButton variant="outline" color="neutral" label="Cancel" @click="showDeleteConfirm = false" />
            <UButton label="Delete" color="error" :disabled="isDemoMode" :loading="deleting" @click="doDeleteTemplate" />
          </div>
        </template>
      </UModal>
    </div>

    <!-- ===================== TEMPLATE MIGRATOR TAB ===================== -->
    <div v-if="activeTab === 'migrator'">

    <!-- Source Project & Team selector -->
    <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>
          <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Source Project</label>
          <USelectMenu autofocus v-model="selectedSourceProjectId" :items="projectOptions" value-key="value"
            :disabled="applying" placeholder="Choose a project…" class="w-full" />
        </div>
        <div>
          <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Source Team</label>
          <USelectMenu v-model="selectedSourceTeam" :items="sourceTeamOptions" value-key="value"
            :loading="loadingSourceTeams" :disabled="!selectedSourceProjectId || applying || loadingSourceTeams"
            placeholder="Choose a team…" class="w-full" />
        </div>
      </div>
    </div>

    <!-- Select Templates -->
    <div v-if="step >= 3" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
      <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-3">
        Select Templates
      </h3>

      <div v-if="loadingTemplates" class="flex items-center gap-2 text-sm text-gray-500">
        <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
        Loading templates…
      </div>

      <div v-else-if="templates.length === 0" class="text-sm text-gray-500 dark:text-gray-400 italic">
        No work item templates found for this team.
      </div>

      <div v-else class="space-y-2">
        <label class="flex items-center gap-3 mb-3 cursor-pointer select-none">
          <UCheckbox :model-value="selectedTemplateIds.length === templates.length" @update:model-value="toggleAllTemplates" />
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">Select all</span>
        </label>
        <div v-for="tmpl in templates" :key="tmpl.id"
          class="flex items-center gap-3 px-3 py-2 rounded-lg border border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/50 cursor-pointer select-none"
          @click="toggleTemplate(tmpl.id)">
          <UCheckbox :model-value="selectedTemplateIds.includes(tmpl.id)" class="pointer-events-none" />
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2">
              <span class="text-sm font-medium text-gray-800 dark:text-gray-100">{{ isDemoMode ? anonPrTitle(tmpl.name) : tmpl.name }}</span>
              <span class="text-xs bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 rounded-full px-2 py-0.5 font-medium shrink-0">
                {{ tmpl.workItemTypeName }}
              </span>
            </div>
            <p v-if="tmpl.description" class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 truncate">{{ tmpl.description }}</p>
          </div>
        </div>
      </div>
    </div>

    <!-- Target Projects & Teams -->
    <div v-if="step >= 4" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
      <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-3">
        Target Projects &amp; Teams
      </h3>
      <label class="block text-xs font-medium text-gray-500 dark:text-gray-400 mb-3">Select one or more projects and teams to copy templates to</label>

      <div v-if="targetProjectOptions.length === 0" class="text-sm text-gray-500 dark:text-gray-400 italic">
        No other configured projects available.
      </div>

      <div v-else class="space-y-2">
        <div v-for="proj in targetProjectOptions" :key="proj.id"
          class="px-3 py-2.5 rounded-lg border border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/50">
          <div class="flex items-center gap-3 cursor-pointer select-none" @click="toggleTargetProject(proj.id)">
            <UCheckbox :model-value="isTargetSelected(proj.id)" class="pointer-events-none" />
            <span class="text-sm font-medium text-gray-800 dark:text-gray-100">{{ proj.label }}</span>
            <UIcon v-if="targetTeamsLoading[proj.id]" name="i-heroicons-arrow-path" class="animate-spin w-3.5 h-3.5 text-gray-400 dark:text-gray-300 ml-auto" />
          </div>
          <div v-if="isTargetSelected(proj.id) && targetTeams[proj.id]?.length" class="mt-2 ml-7 space-y-1">
            <div v-for="team in targetTeamOptions(proj.id)" :key="team.value"
              class="flex items-center gap-2 cursor-pointer select-none px-2 py-1 rounded hover:bg-gray-50 dark:hover:bg-gray-700/40"
              @click="toggleTargetTeam(proj.id, team.value)">
              <UCheckbox :model-value="isTargetTeamSelected(proj.id, team.value)" class="pointer-events-none" />
              <span class="text-sm text-gray-700 dark:text-gray-300">{{ team.label }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Step 5: Preview & Apply -->
    <div v-if="step >= 5 || preview" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
      <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-3">
        Preview &amp; Apply
      </h3>

      <div v-if="loadingPreview && !preview" class="flex items-center gap-2 text-sm text-gray-500">
        <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
        Loading preview…
      </div>

      <div v-if="preview" class="space-y-4">
        <div class="text-xs text-gray-500 dark:text-gray-400 mb-2">
          <UIcon v-if="loadingPreview" name="i-heroicons-arrow-path" class="w-3.5 h-3.5 animate-spin inline-block mr-1 align-text-bottom" />
          Copying <strong class="text-gray-700 dark:text-gray-300">{{ selectedTemplateIds.length }}</strong> template{{ selectedTemplateIds.length !== 1 ? 's' : '' }}
          to <strong class="text-gray-700 dark:text-gray-300">{{ selectedTargetCount }}</strong> project{{ selectedTargetCount !== 1 ? 's' : '' }}
        </div>

        <div class="overflow-x-auto">
          <UTable :data="preview.actions" :columns="previewColumns" />
        </div>

        <!-- Missing fields warning -->
        <div v-if="preview.missing_fields?.length > 0" class="mt-4 rounded-lg border border-amber-200 dark:border-amber-700 bg-amber-50 dark:bg-amber-900/30 px-4 py-3">
          <div class="flex items-start gap-2 mb-2">
            <UIcon name="i-heroicons-exclamation-triangle" class="w-5 h-5 text-amber-600 dark:text-amber-400 mt-0.5 shrink-0" />
            <div>
              <p class="text-sm font-medium text-amber-800 dark:text-amber-200">
                {{ preview.missing_fields.length }} custom field{{ preview.missing_fields.length !== 1 ? 's' : '' }} missing in target project{{ Object.keys(preview.missing_fields_per_target || {}).length !== 1 ? 's' : '' }}
              </p>
              <p class="text-xs text-amber-700 dark:text-amber-300 mt-1">
                Target projects missing these fields will not receive a copy of the affected templates.
              </p>
            </div>
          </div>
          <ul class="ml-7 mt-1 space-y-0.5">
            <li v-for="field in preview.missing_fields" :key="field" class="text-xs text-amber-700 dark:text-amber-300 font-mono">
              {{ field }}
            </li>
          </ul>
        </div>

        <!-- Overwrite option -->
        <div v-if="alreadyExistsCount > 0" class="mt-4 rounded-lg border border-blue-200 dark:border-blue-700 bg-blue-50 dark:bg-blue-900/30 px-4 py-3">
          <div class="flex items-start gap-2">
            <UIcon name="i-heroicons-document-duplicate" class="w-5 h-5 text-blue-600 dark:text-blue-400 mt-0.5 shrink-0" />
            <div>
              <p class="text-sm font-medium text-blue-800 dark:text-blue-200">
                {{ alreadyExistsCount }} template{{ alreadyExistsCount !== 1 ? 's' : '' }} already exist{{ alreadyExistsCount === 1 ? 's' : '' }} in the target
              </p>
              <p class="text-xs text-blue-700 dark:text-blue-300 mt-1">
                By default, a duplicate will be created alongside the existing one.
              </p>
            </div>
          </div>
          <label class="flex items-center gap-2 mt-3 ml-7 cursor-pointer select-none">
            <UCheckbox v-model="overwriteExisting" :disabled="applying || applied || isDemoMode" />
            <span class="text-sm text-blue-800 dark:text-blue-200">Overwrite existing templates instead</span>
          </label>
        </div>

        <!-- Apply section -->
        <div v-if="canApply" class="mt-6 pt-4 border-t border-gray-200 dark:border-gray-700">
          <div class="flex items-center gap-3">
            <UButton
              @click="showConfirmModal = true"
              :disabled="applying || applied"
              :loading="applying"
              :icon="applied ? 'i-heroicons-check-circle' : 'i-heroicons-clipboard-document-list'"
              :label="applying ? 'Copying templates…' : applied ? 'Done!' : `Copy Templates (${willCreateCount + alreadyExistsCount})`"
              :color="applied ? 'success' : 'primary'"
            />

            <UButton v-if="applied" @click="resetAll" variant="outline" color="neutral" label="Reset" />
          </div>

          <UModal :open="showConfirmModal" @update:open="v => { if (!v) showConfirmModal = false }" title="Are you sure?" description="Confirm copying templates">
            <template #body>
              <p class="text-sm text-gray-700 dark:text-gray-300">
                This will copy <strong>{{ willCreateCount + alreadyExistsCount }} template{{ (willCreateCount + alreadyExistsCount) !== 1 ? 's' : '' }}</strong>
                to <strong>{{ selectedTargetCount }} project{{ selectedTargetCount !== 1 ? 's' : '' }}</strong>.
                <span class="text-red-500 font-medium">This action cannot be undone.</span>
              </p>
              <p v-if="isDemoMode" class="text-sm text-amber-600 dark:text-amber-400 mt-2">Disabled in demo mode</p>
            </template>
            <template #footer>
              <div class="flex justify-end gap-2">
                <UButton variant="outline" color="neutral" label="Cancel" @click="showConfirmModal = false" />
                <UButton label="Yes, copy templates" :disabled="isDemoMode" @click="showConfirmModal = false; applyTemplates()" />
              </div>
            </template>
          </UModal>
        </div>

        <!-- Results after apply -->
        <div v-if="applyResult" class="mt-4 space-y-4">
          <!-- Template results -->
          <div>
            <p class="text-xs font-medium text-gray-500 dark:text-gray-400 mb-2 uppercase tracking-wider">Templates</p>
            <div v-if="templateResultCreated.length > 0" class="rounded-lg border border-green-200 dark:border-green-700 bg-green-50 dark:bg-green-900/30 px-4 py-3 mb-2">
              <div class="flex items-start gap-2">
                <UIcon name="i-heroicons-check-circle" class="w-5 h-5 text-green-600 dark:text-green-400 mt-0.5 shrink-0" />
                <div>
                  <p class="text-sm font-medium text-green-800 dark:text-green-200">
                    {{ templateResultCreated.length }} template{{ templateResultCreated.length !== 1 ? 's' : '' }} copied successfully
                  </p>
                  <ul class="mt-1 space-y-0.5">
                    <li v-for="item in templateResultCreated" :key="item.template_name + item.target_project" class="text-xs text-green-700 dark:text-green-300">
                      <span class="font-medium">{{ isDemoMode ? anonPrTitle(item.template_name) : item.template_name }}</span>
                      → {{ isDemoMode ? anonProject(item.target_project) : item.target_project }} ({{ isDemoMode ? anonTeam(item.target_team) : item.target_team }})
                      <span v-if="item.detail" class="italic"> — {{ item.detail }}</span>
                    </li>
                  </ul>
                </div>
              </div>
            </div>
            <div v-if="templateResultOverwritten.length > 0" class="rounded-lg border border-blue-200 dark:border-blue-700 bg-blue-50 dark:bg-blue-900/30 px-4 py-3 mb-2">
              <div class="flex items-start gap-2">
                <UIcon name="i-heroicons-arrow-path" class="w-5 h-5 text-blue-600 dark:text-blue-400 mt-0.5 shrink-0" />
                <div>
                  <p class="text-sm font-medium text-blue-800 dark:text-blue-200">
                    {{ templateResultOverwritten.length }} template{{ templateResultOverwritten.length !== 1 ? 's' : '' }} overwritten
                  </p>
                  <ul class="mt-1 space-y-0.5">
                    <li v-for="item in templateResultOverwritten" :key="item.template_name + item.target_project" class="text-xs text-blue-700 dark:text-blue-300">
                      <span class="font-medium">{{ isDemoMode ? anonPrTitle(item.template_name) : item.template_name }}</span>
                      → {{ isDemoMode ? anonProject(item.target_project) : item.target_project }} ({{ isDemoMode ? anonTeam(item.target_team) : item.target_team }})
                      <span v-if="item.detail" class="italic"> — {{ item.detail }}</span>
                    </li>
                  </ul>
                </div>
              </div>
            </div>
            <div v-if="templateResultSkipped.length > 0" class="rounded-lg border border-amber-200 dark:border-amber-700 bg-amber-50 dark:bg-amber-900/30 px-4 py-3 mb-2">
              <div class="flex items-start gap-2">
                <UIcon name="i-heroicons-minus-circle" class="w-5 h-5 text-amber-600 dark:text-amber-400 mt-0.5 shrink-0" />
                <div>
                  <p class="text-sm font-medium text-amber-800 dark:text-amber-200">
                    {{ templateResultSkipped.length }} template{{ templateResultSkipped.length !== 1 ? 's' : '' }} skipped
                  </p>
                  <ul class="mt-1 space-y-0.5">
                    <li v-for="item in templateResultSkipped" :key="item.template_name + item.target_project" class="text-xs text-amber-700 dark:text-amber-300">
                      <span class="font-medium">{{ isDemoMode ? anonPrTitle(item.template_name) : item.template_name }}</span>
                      → {{ isDemoMode ? anonProject(item.target_project) : item.target_project }} ({{ isDemoMode ? anonTeam(item.target_team) : item.target_team }})
                      <span v-if="item.detail" class="italic"> — {{ item.detail }}</span>
                    </li>
                  </ul>
                </div>
              </div>
            </div>
            <div v-if="templateResultErrors.length > 0" class="rounded-lg border border-red-200 dark:border-red-700 bg-red-50 dark:bg-red-900/30 px-4 py-3">
              <div class="flex items-start gap-2">
                <UIcon name="i-heroicons-x-circle" class="w-5 h-5 text-red-600 dark:text-red-400 mt-0.5 shrink-0" />
                <div>
                  <p class="text-sm font-medium text-red-800 dark:text-red-200">
                    {{ templateResultErrors.length }} template{{ templateResultErrors.length !== 1 ? 's' : '' }} failed
                  </p>
                  <ul class="mt-1 space-y-0.5">
                    <li v-for="item in templateResultErrors" :key="item.template_name + item.target_project" class="text-xs text-red-700 dark:text-red-300">
                      <span class="font-medium">{{ isDemoMode ? anonPrTitle(item.template_name) : item.template_name }}</span>
                      → {{ isDemoMode ? anonProject(item.target_project) : item.target_project }} ({{ isDemoMode ? anonTeam(item.target_team) : item.target_team }})
                      <span v-if="item.detail" class="italic"> — {{ item.detail }}</span>
                    </li>
                  </ul>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Taking longer info -->
    <div v-if="applying && applyingLong" class="mt-4 flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
      <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
      <span>This is taking longer than expected — Azure DevOps is responding slowly…</span>
    </div>

    <!-- Error -->
    <UAlert v-if="error" color="error" icon="i-heroicons-exclamation-triangle" :description="error" class="mt-4" />
    </div>
  </div>
</template>

<script setup>
import { ref, computed, reactive, watch, h, nextTick } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { useApi } from '../composables/useApi.js'
import { useDemoMode, anonTeam, anonPrTitle, anonProject, anonOrg } from '../composables/useDemoMode.js'
const api = useApi()
const store = useMonitorStore()
const { isDemoMode } = useDemoMode()

// ===================== TAB STATE =====================
const tabItems = [
  { label: 'Create Template', value: 'create', icon: 'i-heroicons-plus' },
  { label: 'Template Migrator', value: 'migrator', icon: 'i-heroicons-arrows-right-left' },
]
const activeTab = ref('create')

// ===================== CREATE TEMPLATE STATE =====================
const createProjectId = ref('')
const createTeam = ref('')
const createTeams = ref([])
const loadingCreateTeams = ref(false)
const createWit = ref('')
const createWitOptions = ref([])
const loadingCreateWits = ref(false)
const createTemplates = ref([])
const loadingCreateTemplates = ref(false)
const createName = ref('')
const createDescription = ref('')
const createFields = ref([])
const creating = ref(false)
const created = ref(false)
const createError = ref(null)
const showCreateConfirm = ref(false)
const showDeleteConfirm = ref(false)
const deleteTarget = ref(null)
const deleting = ref(false)
const deleteSuccess = ref(null)
const createSuccess = ref(null)
const witFields = ref([])
const loadingWitFields = ref(false)

const createTeamOptions = computed(() =>
  createTeams.value.map(t => ({ value: t.name, label: isDemoMode.value ? anonTeam(t.name) : t.name }))
)

const availableFieldOptions = computed(() =>
  witFields.value.map(f => ({ value: f.referenceName, label: `${f.name} (${f.referenceName})` }))
)

/** Fields available for a given row — excludes fields already used in other rows */
function availableFieldsFor(index) {
  const usedKeys = createFields.value
    .filter((_, i) => i !== index)
    .map(f => f.key)
    .filter(Boolean)
  return availableFieldOptions.value.filter(o => !usedKeys.includes(o.value))
}

const createStep = computed(() => {
  if (!createProjectId.value || !createTeam.value) return 1
  if (!createWit.value) return 2
  return 3
})

const createBtnRef = ref(null)

const canCreate = computed(() =>
  createProjectId.value && createTeam.value && createWit.value && createName.value.trim().length > 0
)

function addField() {
  createFields.value.push({ key: '', value: '' })
  nextTick(() => {
    createBtnRef.value?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
  })
}

watch(createProjectId, async () => {
  createTeam.value = ''
  createTeams.value = []
  createWit.value = ''
  createWitOptions.value = []
  createTemplates.value = []
  createError.value = null
  created.value = false

  if (!createProjectId.value) return

  loadingCreateTeams.value = true
  loadingCreateWits.value = true
  try {
    const [teams, wits] = await Promise.all([
      api.get(`/api/template-manager/${encodeURIComponent(createProjectId.value)}/teams`),
      api.get(`/api/template-manager/${encodeURIComponent(createProjectId.value)}/work-item-types`)
    ])
    createTeams.value = teams
    createWitOptions.value = wits.map(w => ({ value: w.name, label: w.name }))
    if (teams.length === 1) createTeam.value = teams[0].name
  } catch (e) {
    if (!/unauthorized|401/i.test(e.message)) createError.value = e.message
  } finally {
    loadingCreateTeams.value = false
    loadingCreateWits.value = false
  }
})

watch(createTeam, async () => {
  createTemplates.value = []
  created.value = false
  createError.value = null
  if (!createProjectId.value || !createTeam.value) return
  await loadCreateTemplates()
})

/** Fields to apply after the WIT watcher finishes loading (set by useAsStartingPoint) */
const pendingFields = ref(null)

watch(createWit, async () => {
  witFields.value = []
  createFields.value = []
  created.value = false
  if (!createProjectId.value || !createWit.value) return

  loadingWitFields.value = true
  try {
    witFields.value = await api.get(
      `/api/template-manager/${encodeURIComponent(createProjectId.value)}/work-item-type-fields?workItemType=${encodeURIComponent(createWit.value)}`
    )
  } catch (e) {
    if (!/unauthorized|401/i.test(e.message)) createError.value = e.message
  } finally {
    loadingWitFields.value = false
  }
  // Apply pending fields from useAsStartingPoint after WIT fields have loaded
  if (pendingFields.value) {
    createFields.value = pendingFields.value
    pendingFields.value = null
  }
})

async function loadCreateTemplates(silent = false) {
  if (!silent) loadingCreateTemplates.value = true
  try {
    createTemplates.value = await api.get(
      `/api/template-manager/${encodeURIComponent(createProjectId.value)}/templates?team=${encodeURIComponent(createTeam.value)}`
    )
  } catch (e) {
    if (!/unauthorized|401/i.test(e.message)) createError.value = e.message
  } finally {
    if (!silent) loadingCreateTemplates.value = false
  }
}

async function useAsStartingPoint(tmpl) {
  createError.value = null
  try {
    const full = await api.get(
      `/api/template-manager/${encodeURIComponent(createProjectId.value)}/templates/${encodeURIComponent(tmpl.id)}?team=${encodeURIComponent(createTeam.value)}`
    )
    createName.value = full.name + ' (copy)'
    createDescription.value = full.description || ''
    const fields = Object.entries(full.fields || {}).map(([key, value]) => ({ key, value }))
    if (full.workItemTypeName !== createWit.value) {
      // WIT will change — watcher will reset createFields, so stash them
      pendingFields.value = fields
      createWit.value = full.workItemTypeName
    } else {
      // Same WIT — watcher won't fire, apply fields directly
      createFields.value = fields
    }
    created.value = false
  } catch (e) {
    createError.value = e.message
  }
}

function confirmDeleteTemplate(tmpl) {
  deleteTarget.value = tmpl
  showDeleteConfirm.value = true
}

async function doDeleteTemplate() {
  deleting.value = true
  createError.value = null
  deleteSuccess.value = null
  try {
    await api.del(
      `/api/template-manager/${encodeURIComponent(createProjectId.value)}/templates/${encodeURIComponent(deleteTarget.value.id)}?team=${encodeURIComponent(createTeam.value)}`
    )
    const deletedName = deleteTarget.value.name
    createTemplates.value = createTemplates.value.filter(t => t.id !== deleteTarget.value.id)
    showDeleteConfirm.value = false
    deleteTarget.value = null
    deleteSuccess.value = `Template "${deletedName}" deleted successfully.`
    setTimeout(() => { deleteSuccess.value = null }, 4000)
  } catch (e) {
    showDeleteConfirm.value = false
    createError.value = e.message
  } finally {
    deleting.value = false
  }
}

async function doCreateTemplate() {
  creating.value = true
  createError.value = null
  try {
    const fields = {}
    for (const f of createFields.value) {
      if (f.key.trim()) fields[f.key.trim()] = f.value
    }
    await api.post(`/api/template-manager/${encodeURIComponent(createProjectId.value)}/templates/create`, {
      team: createTeam.value,
      name: createName.value.trim(),
      description: createDescription.value.trim(),
      work_item_type_name: createWit.value,
      fields
    })
    showCreateConfirm.value = false
    created.value = true
    createSuccess.value = `Template "${createName.value.trim()}" created successfully.`
    setTimeout(() => { createSuccess.value = null }, 4000)
    // Optimistically show the new template, then reload to get the real ID
    const optimisticName = createName.value.trim()
    const optimisticWit = createWit.value
    createTemplates.value = [...createTemplates.value, {
      id: `_pending_${Date.now()}`,
      name: optimisticName,
      workItemTypeName: optimisticWit
    }]
    // Reload in the background — once Azure DevOps catches up the real ID replaces the placeholder
    api.invalidate(`/api/template-manager/${encodeURIComponent(createProjectId.value)}/templates`)
    setTimeout(async () => { await loadCreateTemplates(true) }, 1500)
  } catch (e) {
    createError.value = e.message
    showCreateConfirm.value = false
  } finally {
    creating.value = false
  }
}

function resetCreateForm() {
  createName.value = ''
  createDescription.value = ''
  createFields.value = []
  created.value = false
  createError.value = null
}

// ===================== MIGRATOR STATE =====================

const previewColumns = [
  {
    accessorKey: 'template_name',
    header: 'Template',
    cell: ({ row }) => isDemoMode.value ? anonPrTitle(row.original.template_name) : row.original.template_name
  },
  {
    accessorKey: 'work_item_type',
    header: 'Type',
    cell: ({ row }) => h('span', { class: 'text-xs bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 rounded-full px-2 py-0.5 font-medium' }, row.original.work_item_type)
  },
  { accessorKey: 'target_project', header: 'Target Project' },
  {
    accessorKey: 'target_team',
    header: 'Team',
    cell: ({ row }) => isDemoMode.value ? anonTeam(row.original.target_team) : row.original.target_team
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h('span', {
      class: row.original.status === 'will_create'
        ? 'text-xs bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-300 rounded-full px-2 py-0.5 font-medium'
        : 'text-xs bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300 rounded-full px-2 py-0.5 font-medium'
    }, row.original.status === 'will_create' ? 'Will create' : 'Already exists')
  },
]

const projects = ref([])
const sourceTeams = ref([])
const templates = ref([])
const preview = ref(null)
const applyResult = ref(null)

const selectedSourceProjectId = ref('')
const selectedSourceTeam = ref('')
const selectedTemplateIds = ref([])
const targetSelectedTeams = reactive({})
const targetTeams = reactive({})
const targetTeamsLoading = reactive({})
const selectedTargets = ref([])

const loadingSourceTeams = ref(false)
const loadingTemplates = ref(false)
const loadingPreview = ref(false)
const applying = ref(false)
const applied = ref(false)
const showConfirmModal = ref(false)
const overwriteExisting = ref(false)
const error = ref(null)
const applyingLong = ref(false)
let applyingTimer = null

const projectOptions = computed(() =>
  projects.value.map(p => ({ value: p.id, label: isDemoMode.value ? `${anonProject(p.project)} (${anonOrg(p.organization)})` : `${p.project} (${p.organization})` }))
)

const sourceTeamOptions = computed(() =>
  sourceTeams.value.map(t => ({ value: t.name, label: isDemoMode.value ? anonTeam(t.name) : t.name }))
)

const targetProjectOptions = computed(() =>
  projects.value
    .filter(p => p.id !== selectedSourceProjectId.value)
    .map(p => ({ id: p.id, label: isDemoMode.value ? `${anonProject(p.project)} (${anonOrg(p.organization)})` : `${p.project} (${p.organization})` }))
)

const selectedTargetCount = computed(() => selectedTargets.value.length)

const willCreateCount = computed(() => {
  if (!preview.value) return 0
  return preview.value.actions.filter(a => a.status === 'will_create').length
})

const alreadyExistsCount = computed(() => {
  if (!preview.value) return 0
  return preview.value.actions.filter(a => a.status === 'already_exists').length
})

const canApply = computed(() => willCreateCount.value > 0 || alreadyExistsCount.value > 0)

// Apply result groupings
const templateResultCreated = computed(() =>
  (applyResult.value?.results || []).filter(r => r.status === 'created')
)
const templateResultOverwritten = computed(() =>
  (applyResult.value?.results || []).filter(r => r.status === 'overwritten')
)
const templateResultSkipped = computed(() =>
  (applyResult.value?.results || []).filter(r => r.status === 'skipped')
)
const templateResultErrors = computed(() =>
  (applyResult.value?.results || []).filter(r => r.status === 'error')
)

function targetTeamOptions(projectId) {
  const teams = targetTeams[projectId] || []
  return teams.map(t => ({ value: t.name, label: isDemoMode.value ? anonTeam(t.name) : t.name }))
}

function isTargetSelected(projectId) {
  return selectedTargets.value.includes(projectId)
}

function isTargetTeamSelected(projectId, teamName) {
  return (targetSelectedTeams[projectId] || []).includes(teamName)
}

function toggleTargetTeam(projectId, teamName) {
  if (!targetSelectedTeams[projectId]) targetSelectedTeams[projectId] = []
  const idx = targetSelectedTeams[projectId].indexOf(teamName)
  if (idx >= 0) {
    targetSelectedTeams[projectId].splice(idx, 1)
  } else {
    targetSelectedTeams[projectId].push(teamName)
  }
}

const step = computed(() => {
  if (!selectedSourceProjectId.value) return 1
  if (!selectedSourceTeam.value) return 2
  if (selectedTemplateIds.value.length === 0) return 3
  if (selectedTargets.value.length === 0) return 4
  const hasTeam = selectedTargets.value.some(id => (targetSelectedTeams[id] || []).length > 0)
  if (!hasTeam) return 4
  return 5
})

;(async () => {
  try {
    projects.value = await api.get('/api/template-manager/projects')
  } catch (e) {
    error.value = e.message
  }
})()

watch(selectedSourceProjectId, () => onSourceProjectChange())
watch(selectedSourceTeam, () => onSourceTeamChange())

// Auto-load preview when step 5 is reached or inputs change (debounced)
let previewDebounce = null
watch([selectedTemplateIds, selectedTargets, () => JSON.stringify(targetSelectedTeams)], () => {
  // Clear stale errors and results when selections change
  error.value = null
  applyResult.value = null
  applied.value = false
  clearTimeout(previewDebounce)
  if (step.value >= 5) {
    previewDebounce = setTimeout(() => loadPreview(), 400)
  }
})

async function onSourceProjectChange() {
  sourceTeams.value = []
  templates.value = []
  preview.value = null
  applyResult.value = null
  selectedSourceTeam.value = ''
  selectedTemplateIds.value = []
  selectedTargets.value = []
  applied.value = false
  error.value = null

  if (!selectedSourceProjectId.value) return

  loadingSourceTeams.value = true
  try {
    sourceTeams.value = await api.get(`/api/template-manager/${encodeURIComponent(selectedSourceProjectId.value)}/teams`)
    if (sourceTeams.value.length === 1) {
      selectedSourceTeam.value = sourceTeams.value[0].name
      await onSourceTeamChange()
    }
  } catch (e) {
    if (!/unauthorized|401/i.test(e.message)) error.value = e.message
  } finally {
    loadingSourceTeams.value = false
  }
}

async function onSourceTeamChange() {
  templates.value = []
  preview.value = null
  applyResult.value = null
  selectedTemplateIds.value = []
  selectedTargets.value = []
  applied.value = false
  error.value = null

  if (!selectedSourceTeam.value) return

  loadingTemplates.value = true
  try {
    templates.value = await api.get(
      `/api/template-manager/${encodeURIComponent(selectedSourceProjectId.value)}/templates?team=${encodeURIComponent(selectedSourceTeam.value)}`
    )
  } catch (e) {
    if (!/unauthorized|401/i.test(e.message)) error.value = e.message
  } finally {
    loadingTemplates.value = false
  }
}

function toggleTemplate(id) {
  const idx = selectedTemplateIds.value.indexOf(id)
  if (idx >= 0) {
    selectedTemplateIds.value.splice(idx, 1)
  } else {
    selectedTemplateIds.value.push(id)
  }
  preview.value = null
  applyResult.value = null
  applied.value = false
}

function toggleAllTemplates() {
  if (selectedTemplateIds.value.length === templates.value.length) {
    selectedTemplateIds.value = []
  } else {
    selectedTemplateIds.value = templates.value.map(t => t.id)
  }
  preview.value = null
  applyResult.value = null
  applied.value = false
}

async function toggleTargetProject(projectId) {
  applyResult.value = null
  applied.value = false

  const idx = selectedTargets.value.indexOf(projectId)
  if (idx >= 0) {
    selectedTargets.value.splice(idx, 1)
    return
  }

  selectedTargets.value.push(projectId)

  if (!targetTeams[projectId]) {
    targetTeamsLoading[projectId] = true
    try {
      const teams = await api.get(`/api/template-manager/${encodeURIComponent(projectId)}/teams`)
      targetTeams[projectId] = teams
      if (teams.length > 0) {
        targetSelectedTeams[projectId] = [teams[0].name]
      }
    } catch (e) {
      if (!/unauthorized|401/i.test(e.message)) error.value = e.message
    } finally {
      targetTeamsLoading[projectId] = false
    }
  }
}

async function loadPreview() {
  applyResult.value = null
  applied.value = false
  error.value = null
  loadingPreview.value = true

  try {
    preview.value = await api.post('/api/template-manager/preview', {
      source_project_id: selectedSourceProjectId.value,
      source_team: selectedSourceTeam.value,
      template_ids: selectedTemplateIds.value,
      targets: selectedTargets.value.flatMap(id =>
        (targetSelectedTeams[id] || []).map(team => ({ project_id: id, team }))
      )
    })
  } catch (e) {
    if (!/unauthorized|401/i.test(e.message)) error.value = e.message
  } finally {
    loadingPreview.value = false
  }
}

async function applyTemplates() {
  applying.value = true
  applyingLong.value = false
  error.value = null
  applyingTimer = setTimeout(() => { applyingLong.value = true }, 10000)

  try {
    applyResult.value = await api.post('/api/template-manager/apply', {
      source_project_id: selectedSourceProjectId.value,
      source_team: selectedSourceTeam.value,
      template_ids: selectedTemplateIds.value,
      overwrite_existing: overwriteExisting.value,
      targets: selectedTargets.value.flatMap(id =>
        (targetSelectedTeams[id] || []).map(team => ({ project_id: id, team }))
      )
    })
    applied.value = true
  } catch (e) {
    error.value = e.message
  } finally {
    clearTimeout(applyingTimer)
    applying.value = false
    applyingLong.value = false
  }
}

function resetAll() {
  selectedSourceProjectId.value = ''
  selectedSourceTeam.value = ''
  sourceTeams.value = []
  templates.value = []
  selectedTemplateIds.value = []
  selectedTargets.value = []
  preview.value = null
  applyResult.value = null
  applied.value = false
  applying.value = false
  applyingLong.value = false
  error.value = null
}
</script>
