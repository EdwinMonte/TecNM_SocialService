import assert from 'node:assert/strict'
import fs from 'node:fs'
import {pathToFileURL} from 'node:url'
const {chromium}=await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE).href)
const browser=await chromium.launch({channel:'msedge',headless:true})
const base='https://127.0.0.1:5900'
const output='tmp/pdf-qa/follow-up';fs.mkdirSync(output,{recursive:true})
try {
 const context=await browser.newContext({ignoreHTTPSErrors:true,viewport:{width:1440,height:1000}})
 await context.addCookies([{name:"session_exp",value:String(Math.floor(Date.now()/1000)+3600),url:base,secure:true}]);
 const page=await context.newPage(); const errors=[],queries=[],reviews=[];page.on('pageerror',e=>{errors.push(e.message);console.log('PAGE ERROR',e.message)})
 let role='social_service_officer',fail=false
 const statuses=['first_notice','second_notice','third_notice','non_compliant','in_progress','released','pending_setup']
 const rows=statuses.map((status,i)=>({id:i+1,controlNumber:'FOLLOW'+i,fullName:'Alumno '+i,careerId:1,careerCode:'INF',currentSemester:null,curriculumProgressPercentage:null,shift:null,isEnrolled:null,hasValidImss:null,hasUserAccount:null,accountStatus:null,canReadProfile:false,socialServiceFollowUp:{socialServiceId:i+1,eligiblePeriodCode:'FJ25',trackingPeriodId:2,trackingPeriodCode:'AD26',approvedDocumentsCount:status==='in_progress'?1:0,status,progressAssessment:null,notes:null}}))
 await page.route('**/api/**',async route=>{
  const url=new URL(route.request().url());if(!url.pathname.startsWith('/api/'))return route.continue();let body=[]
  if(url.pathname==='/api/auth/me')body={id:1,username:'qa',name:'Prueba',roles:[role]}
  else if(url.pathname.endsWith('/social-service-follow-up/review')) {
   if(fail)return route.fulfill({status:409,json:{detail:'Actualiza el listado.'}})
   const payload=route.request().postDataJSON();reviews.push(payload);rows[4].socialServiceFollowUp.progressAssessment=payload.assessment;body={}
  }else if(url.pathname==='/api/students'){
   queries.push(Object.fromEntries(url.searchParams));const items=role==='internship_coordinator'?[]:rows
   body={items,totalItems:items.length,totalPages:1,page:1,pageSize:50}
  }else if(url.pathname.includes('academic-periods/options'))body=[{id:2,label:'AD26',meta:{isActive:true}}]
  else if(url.pathname.includes('review-queue'))body={items:[],totalPages:0}
  await route.fulfill({json:body})
 })
 await page.goto(base+'/students');await page.locator('.roster-status').first().waitFor()
 assert.equal(await page.getByRole('button',{name:'Consultar / IMSS'}).count(),0)
 await page.getByRole('combobox',{name:'Seguimiento SS',exact:true}).selectOption('third_notice')
 await page.waitForTimeout(200);assert.equal(queries.at(-1).followUpStatus,'third_notice')
 await page.getByRole('button',{name:'Ver detalle de Alumno 4',exact:true}).click()
 await page.getByRole('button',{name:'Valorar seguimiento'}).click()
 await page.getByRole('combobox',{name:'Constancia',exact:true}).selectOption('low_progress')
 fail=true;await page.getByRole('button',{name:'Guardar valoración'}).click();await page.getByRole('alert').filter({hasText:'Actualiza el listado.'}).waitFor()
 fail=false;await page.getByRole('button',{name:'Guardar valoración'}).click();await page.waitForTimeout(200)
 assert.equal(reviews[0].assessment,'low_progress');assert.equal(reviews[0].periodId,2)
 await page.screenshot({path:output+'/desktop.png',fullPage:true})
 await page.setViewportSize({width:390,height:844});await page.screenshot({path:output+'/mobile.png',fullPage:true})
 role='internship_coordinator';await page.reload();await page.waitForTimeout(400)
 assert.equal(await page.getByRole('combobox',{name:'Seguimiento SS',exact:true}).count(),0)
 assert.deepEqual(errors,[]);console.log('Follow-up UI statuses, filters, review conflict/save, redaction, mobile and PP visibility passed.')
} finally {await browser.close()}



