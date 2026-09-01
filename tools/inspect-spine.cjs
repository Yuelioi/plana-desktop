const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function usage() {
  console.error('Usage: node tools/inspect-spine.cjs <spine-player.js> <skeleton.skel> <texture.atlas>');
  process.exitCode = 2;
}

if (process.argv.length !== 5) {
  usage();
  return;
}

const [, , runtimePath, skeletonPath, atlasPath] = process.argv;
for (const inputPath of [runtimePath, skeletonPath, atlasPath]) {
  if (!fs.existsSync(inputPath)) {
    console.error(`File not found: ${inputPath}`);
    process.exitCode = 2;
    return;
  }
}

const context = vm.createContext({
  console,
  setTimeout,
  clearTimeout,
  TextDecoder,
  Uint8Array,
  ArrayBuffer,
});
const runtimeSource = `${fs.readFileSync(runtimePath, 'utf8')}\n;globalThis.__spine = spine;`;
vm.runInContext(runtimeSource, context, { filename: path.resolve(runtimePath) });

const spine = context.__spine;
const atlas = new spine.TextureAtlas(fs.readFileSync(atlasPath, 'utf8'));
const loader = new spine.SkeletonBinary(new spine.AtlasAttachmentLoader(atlas));
const bytes = fs.readFileSync(skeletonPath);
const data = loader.readSkeletonData(new Uint8Array(bytes.buffer, bytes.byteOffset, bytes.byteLength));

function attachmentNames(skin) {
  const names = [];
  for (let slotIndex = 0; slotIndex < data.slots.length; slotIndex += 1) {
    const slotNames = [];
    skin.getAttachmentsForSlot(slotIndex, slotNames);
    for (const entry of slotNames) names.push({ slot: data.slots[slotIndex].name, name: entry.name });
  }
  return names;
}

const report = {
  source: path.basename(skeletonPath),
  version: data.version,
  hash: data.hash,
  bounds: { x: data.x, y: data.y, width: data.width, height: data.height },
  counts: {
    bones: data.bones.length,
    slots: data.slots.length,
    skins: data.skins.length,
    attachments: data.skins.reduce((total, skin) => total + attachmentNames(skin).length, 0),
    animations: data.animations.length,
    events: data.events.length,
    ikConstraints: data.ikConstraints.length,
    transformConstraints: data.transformConstraints.length,
    pathConstraints: data.pathConstraints.length,
  },
  animations: data.animations.map((animation) => ({
    name: animation.name,
    duration: animation.duration,
    boneTimelines: animation.timelines.filter((timeline) => Number.isInteger(timeline.boneIndex)).length,
    constraintTimelines: animation.timelines.filter((timeline) => Number.isInteger(timeline.ikConstraintIndex) || Number.isInteger(timeline.transformConstraintIndex) || Number.isInteger(timeline.pathConstraintIndex)).length,
    timelineTypes: [...new Set(animation.timelines.map((timeline) => timeline.constructor?.name || 'unknown'))],
    attachmentChanges: animation.timelines
      .filter((timeline) => Array.isArray(timeline.attachmentNames))
      .map((timeline) => ({
        slot: data.slots[timeline.slotIndex].name,
        attachments: [...new Set(timeline.attachmentNames)],
      })),
  })),
  skins: data.skins.map((skin) => ({ name: skin.name, attachments: attachmentNames(skin) })),
  bones: data.bones.map((bone) => ({ name: bone.name, parent: bone.parent?.name || null })),
  slots: data.slots.map((slot) => ({ name: slot.name, bone: slot.boneData?.name || slot.bone?.name || null, attachment: slot.attachmentName })),
  events: data.events.map((event) => ({
    name: event.name,
    intValue: event.intValue,
    floatValue: event.floatValue,
    stringValue: event.stringValue,
    audioPath: event.audioPath,
  })),
};

process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
